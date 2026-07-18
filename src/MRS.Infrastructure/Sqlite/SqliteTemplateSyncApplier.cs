using Microsoft.Data.Sqlite;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

internal static class SqliteTemplateSyncApplier
{
	internal static async Task UpsertAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		TemplateSyncPayload payload,
		CancellationToken cancellationToken)
	{
		await EnsureMaintenanceTypeAsync(connection, tx, payload, cancellationToken).ConfigureAwait(false);
		await UpsertTemplateAsync(connection, tx, payload, cancellationToken).ConfigureAwait(false);
		await ReplaceFieldsAsync(connection, tx, payload.LocalId, payload.Fields, cancellationToken).ConfigureAwait(false);
	}

	private static async Task EnsureMaintenanceTypeAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		TemplateSyncPayload payload,
		CancellationToken cancellationToken)
	{
		using var find = connection.CreateCommand();
		find.Transaction = tx;
		find.CommandText = "SELECT 1 FROM maintenance_types WHERE id = $id LIMIT 1;";
		find.Parameters.AddWithValue("$id", payload.MaintenanceTypeId);
		if (await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
			return;

		var name = payload.MaintenanceTypeName ?? $"ТО {payload.MaintenanceTypeId}";
		var code = payload.MaintenanceTypeCode ?? $"SYNC-{payload.MaintenanceTypeId}";
		using var ins = connection.CreateCommand();
		ins.Transaction = tx;
		ins.CommandText = """
			INSERT INTO maintenance_types (id, type_name, code)
			VALUES ($id, $name, $code)
			ON CONFLICT(id) DO UPDATE SET type_name = excluded.type_name, code = excluded.code;
			""";
		ins.Parameters.AddWithValue("$id", payload.MaintenanceTypeId);
		ins.Parameters.AddWithValue("$name", name);
		ins.Parameters.AddWithValue("$code", code);
		await ins.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task UpsertTemplateAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		TemplateSyncPayload payload,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.Transaction = tx;
		cmd.CommandText = """
			INSERT INTO checklist_templates (
			    id, facility_id, equipment_type_id, maintenance_type_id, template_name, scenario_code, version,
			    is_active, top_plate_text, intro_modal_text, safety_modal_text, red_button_enabled)
			VALUES ($id, $fid, $et, $mt, $name, $scenario, $version, 1, $top, $intro, $safety, $red)
			ON CONFLICT(id) DO UPDATE SET
			    facility_id = excluded.facility_id,
			    equipment_type_id = excluded.equipment_type_id,
			    maintenance_type_id = excluded.maintenance_type_id,
			    template_name = excluded.template_name,
			    scenario_code = excluded.scenario_code,
			    version = excluded.version,
			    top_plate_text = excluded.top_plate_text,
			    intro_modal_text = excluded.intro_modal_text,
			    safety_modal_text = excluded.safety_modal_text,
			    red_button_enabled = excluded.red_button_enabled;
			""";
		cmd.Parameters.AddWithValue("$id", payload.LocalId);
		cmd.Parameters.AddWithValue("$fid", payload.FacilityId is int fid ? fid : DBNull.Value);
		cmd.Parameters.AddWithValue("$et", payload.EquipmentTypeId);
		cmd.Parameters.AddWithValue("$mt", payload.MaintenanceTypeId);
		cmd.Parameters.AddWithValue("$name", payload.TemplateName);
		cmd.Parameters.AddWithValue("$scenario", payload.ScenarioCode ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$version", payload.Version);
		cmd.Parameters.AddWithValue("$top", payload.TopPlateText ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$intro", payload.IntroModalText ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$safety", payload.SafetyModalText ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$red", payload.RedButtonEnabled ? 1 : 0);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task ReplaceFieldsAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		int templateId,
		IReadOnlyList<TemplateFieldSyncPayload> fields,
		CancellationToken cancellationToken)
	{
		using (var del = connection.CreateCommand())
		{
			del.Transaction = tx;
			del.CommandText = "DELETE FROM checklist_template_items WHERE checklist_template_id = $id;";
			del.Parameters.AddWithValue("$id", templateId);
			await del.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		var typeMap = await LoadFieldTypeMapAsync(connection, tx, cancellationToken).ConfigureAwait(false);

		foreach (var field in fields.OrderBy(f => f.SortOrder))
		{
			if (!typeMap.TryGetValue(field.FieldTypeName, out var typeId))
				throw new InvalidOperationException($"Неизвестный тип поля: {field.FieldTypeName}");

			long itemId;
			using (var ins = connection.CreateCommand())
			{
				ins.Transaction = tx;
				ins.CommandText = """
					INSERT INTO checklist_template_items (
					    checklist_template_id, sort_order, field_code, question_text, hint_text,
					    field_type_id, validation_rule_code, is_required, group_name)
					VALUES ($tid, $sort, $code, $question, $hint, $ft, $validation, $required, $group);
					SELECT last_insert_rowid();
					""";
				ins.Parameters.AddWithValue("$tid", templateId);
				ins.Parameters.AddWithValue("$sort", field.SortOrder);
				ins.Parameters.AddWithValue("$code", field.FieldCode ?? (object)DBNull.Value);
				ins.Parameters.AddWithValue("$question", field.QuestionText);
				ins.Parameters.AddWithValue("$hint", field.HintText ?? (object)DBNull.Value);
				ins.Parameters.AddWithValue("$ft", typeId);
				ins.Parameters.AddWithValue("$validation", field.ValidationRuleCode ?? (object)DBNull.Value);
				ins.Parameters.AddWithValue("$required", field.IsRequired ? 1 : 0);
				ins.Parameters.AddWithValue("$group", field.GroupName ?? (object)DBNull.Value);
				var scalar = await ins.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
				itemId = scalar is long l ? l : Convert.ToInt64(scalar ?? throw new InvalidOperationException("Не удалось создать поле шаблона."));
			}

			var sort = 1;
			foreach (var option in field.Options)
			{
				using var opt = connection.CreateCommand();
				opt.Transaction = tx;
				opt.CommandText = """
					INSERT INTO checklist_template_item_options (checklist_template_item_id, sort_order, option_label)
					VALUES ($iid, $sort, $label);
					""";
				opt.Parameters.AddWithValue("$iid", itemId);
				opt.Parameters.AddWithValue("$sort", sort++);
				opt.Parameters.AddWithValue("$label", option);
				await opt.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private static async Task<Dictionary<string, int>> LoadFieldTypeMapAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		CancellationToken cancellationToken)
	{
		var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		using var cmd = connection.CreateCommand();
		cmd.Transaction = tx;
		cmd.CommandText = "SELECT id, type_name FROM field_types;";
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			map[reader.GetString(1)] = reader.GetInt32(0);
		return map;
	}
}
