using System.Text.Json;
using MRS.Application.Sync;
using Npgsql;

namespace MRS.Infrastructure.Postgres;

internal static class PostgresSyncTemplateWriter
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	internal static async Task UpsertAsync(NpgsqlConnection connection, TemplateSyncPayload payload, CancellationToken cancellationToken)
	{
		await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		var maintenanceTypeId = await EnsureMaintenanceTypeAsync(connection, tx, payload, cancellationToken).ConfigureAwait(false);
		var templateId = await UpsertTemplateAsync(connection, tx, payload, maintenanceTypeId, cancellationToken).ConfigureAwait(false);
		await ReplaceFieldsAsync(connection, tx, templateId, payload.Fields, cancellationToken).ConfigureAwait(false);

		await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
	}

	internal static TemplateSyncPayload Parse(string json) =>
		JsonSerializer.Deserialize<TemplateSyncPayload>(json, JsonOptions)
		?? throw new InvalidOperationException("Не удалось разобрать template payload.");

	private static async Task<int> EnsureMaintenanceTypeAsync(
		NpgsqlConnection connection, NpgsqlTransaction tx, TemplateSyncPayload payload, CancellationToken cancellationToken)
	{
		await using (var find = new NpgsqlCommand("SELECT id FROM maintenance_types WHERE id = @id;", connection, tx))
		{
			find.Parameters.AddWithValue("id", payload.MaintenanceTypeId);
			if (await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
				return payload.MaintenanceTypeId;
		}

		var name = payload.MaintenanceTypeName ?? $"ТО {payload.MaintenanceTypeId}";
		var code = payload.MaintenanceTypeCode ?? $"INT-SYNC-{payload.MaintenanceTypeId}";

		await using var cmd = new NpgsqlCommand("""
			INSERT INTO maintenance_types (id, type_name, code)
			VALUES (@id, @name, @code)
			ON CONFLICT (id) DO UPDATE SET type_name = EXCLUDED.type_name, code = EXCLUDED.code;
			""", connection, tx);
		cmd.Parameters.AddWithValue("id", payload.MaintenanceTypeId);
		cmd.Parameters.AddWithValue("name", name);
		cmd.Parameters.AddWithValue("code", code);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		await BumpSequenceAsync(connection, tx, "maintenance_types", payload.MaintenanceTypeId, cancellationToken).ConfigureAwait(false);
		return payload.MaintenanceTypeId;
	}

	private static async Task<int> UpsertTemplateAsync(
		NpgsqlConnection connection,
		NpgsqlTransaction tx,
		TemplateSyncPayload payload,
		int maintenanceTypeId,
		CancellationToken cancellationToken)
	{
		await using var cmd = new NpgsqlCommand("""
			INSERT INTO checklist_templates (
			    id, equipment_type_id, maintenance_type_id, template_name, scenario_code, version,
			    is_active, top_plate_text, intro_modal_text, safety_modal_text, red_button_enabled)
			VALUES (@id, @et, @mt, @name, @scenario, @version, TRUE, @top, @intro, @safety, @red)
			ON CONFLICT (id) DO UPDATE SET
			    equipment_type_id = EXCLUDED.equipment_type_id,
			    maintenance_type_id = EXCLUDED.maintenance_type_id,
			    template_name = EXCLUDED.template_name,
			    scenario_code = EXCLUDED.scenario_code,
			    version = EXCLUDED.version,
			    top_plate_text = EXCLUDED.top_plate_text,
			    intro_modal_text = EXCLUDED.intro_modal_text,
			    safety_modal_text = EXCLUDED.safety_modal_text,
			    red_button_enabled = EXCLUDED.red_button_enabled;
			""", connection, tx);
		cmd.Parameters.AddWithValue("id", payload.LocalId);
		cmd.Parameters.AddWithValue("et", payload.EquipmentTypeId);
		cmd.Parameters.AddWithValue("mt", maintenanceTypeId);
		cmd.Parameters.AddWithValue("name", payload.TemplateName);
		cmd.Parameters.AddWithValue("scenario", payload.ScenarioCode ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("version", payload.Version);
		cmd.Parameters.AddWithValue("top", payload.TopPlateText ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("intro", payload.IntroModalText ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("safety", payload.SafetyModalText ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("red", payload.RedButtonEnabled);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		await BumpSequenceAsync(connection, tx, "checklist_templates", payload.LocalId, cancellationToken).ConfigureAwait(false);
		return payload.LocalId;
	}

	private static async Task ReplaceFieldsAsync(
		NpgsqlConnection connection,
		NpgsqlTransaction tx,
		int templateId,
		IReadOnlyList<TemplateFieldSyncPayload> fields,
		CancellationToken cancellationToken)
	{
		await using (var del = new NpgsqlCommand("DELETE FROM checklist_template_items WHERE checklist_template_id = @id;", connection, tx))
		{
			del.Parameters.AddWithValue("id", templateId);
			await del.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		var typeMap = await LoadFieldTypeMapAsync(connection, tx, cancellationToken).ConfigureAwait(false);

		foreach (var field in fields.OrderBy(f => f.SortOrder))
		{
			if (!typeMap.TryGetValue(field.FieldTypeName.ToLowerInvariant(), out var typeId))
				throw new InvalidOperationException($"Неизвестный тип поля: {field.FieldTypeName}");

			long itemId;
			await using (var ins = new NpgsqlCommand("""
				INSERT INTO checklist_template_items (
				    checklist_template_id, sort_order, field_code, question_text, hint_text,
				    field_type_id, validation_rule_code, is_required, group_name)
				VALUES (@tid, @sort, @code, @question, @hint, @ft, @validation, @required, @group)
				RETURNING id;
				""", connection, tx))
			{
				ins.Parameters.AddWithValue("tid", templateId);
				ins.Parameters.AddWithValue("sort", field.SortOrder);
				ins.Parameters.AddWithValue("code", field.FieldCode ?? (object)DBNull.Value);
				ins.Parameters.AddWithValue("question", field.QuestionText);
				ins.Parameters.AddWithValue("hint", field.HintText ?? (object)DBNull.Value);
				ins.Parameters.AddWithValue("ft", typeId);
				ins.Parameters.AddWithValue("validation", field.ValidationRuleCode ?? (object)DBNull.Value);
				ins.Parameters.AddWithValue("required", field.IsRequired);
				ins.Parameters.AddWithValue("group", field.GroupName ?? (object)DBNull.Value);
				itemId = Convert.ToInt64(await ins.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
			}

			var sort = 1;
			foreach (var option in field.Options)
			{
				await using var opt = new NpgsqlCommand("""
					INSERT INTO checklist_template_item_options (checklist_template_item_id, sort_order, option_label)
					VALUES (@iid, @sort, @label);
					""", connection, tx);
				opt.Parameters.AddWithValue("iid", itemId);
				opt.Parameters.AddWithValue("sort", sort++);
				opt.Parameters.AddWithValue("label", option);
				await opt.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private static async Task<Dictionary<string, int>> LoadFieldTypeMapAsync(
		NpgsqlConnection connection, NpgsqlTransaction tx, CancellationToken cancellationToken)
	{
		var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		await using var cmd = new NpgsqlCommand("SELECT id, type_name FROM field_types;", connection, tx);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			map[reader.GetString(1)] = reader.GetInt32(0);
		return map;
	}

	private static async Task BumpSequenceAsync(
		NpgsqlConnection connection, NpgsqlTransaction tx, string table, int id, CancellationToken cancellationToken)
	{
		await using var cmd = new NpgsqlCommand(
			$"SELECT setval(pg_get_serial_sequence('{table}', 'id'), GREATEST((SELECT COALESCE(MAX(id), 0) FROM {table}), @id));",
			connection, tx);
		cmd.Parameters.AddWithValue("id", id);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}
}
