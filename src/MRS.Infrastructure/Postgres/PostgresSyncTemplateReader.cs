using MRS.Application.Sync;
using Npgsql;

namespace MRS.Infrastructure.Postgres;

internal static class PostgresSyncTemplateReader
{
	internal static async Task<IReadOnlyList<TemplateSyncPayload>> ReadAllAsync(
		NpgsqlConnection connection,
		CancellationToken cancellationToken)
	{
		var ids = new List<int>();
		await using (var cmd = new NpgsqlCommand("SELECT id FROM checklist_templates ORDER BY id;", connection))
		{
			await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				ids.Add(reader.GetInt32(0));
		}

		var list = new List<TemplateSyncPayload>(ids.Count);
		foreach (var id in ids)
			list.Add(await ReadOneAsync(connection, id, cancellationToken).ConfigureAwait(false));
		return list;
	}

	private static async Task<TemplateSyncPayload> ReadOneAsync(
		NpgsqlConnection connection,
		int templateId,
		CancellationToken cancellationToken)
	{
		await using var headerCmd = new NpgsqlCommand("""
			SELECT ct.equipment_type_id, ct.maintenance_type_id, ct.template_name, ct.scenario_code, ct.version,
			       ct.top_plate_text, ct.intro_modal_text, ct.safety_modal_text, ct.red_button_enabled,
			       mt.type_name, mt.code, ct.facility_id
			FROM checklist_templates ct
			INNER JOIN maintenance_types mt ON mt.id = ct.maintenance_type_id
			WHERE ct.id = @id;
			""", connection);
		headerCmd.Parameters.AddWithValue("id", templateId);
		await using var headerReader = await headerCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await headerReader.ReadAsync(cancellationToken).ConfigureAwait(false))
			throw new InvalidOperationException($"Шаблон {templateId} не найден.");

		var equipmentTypeId = headerReader.GetInt32(0);
		var maintenanceTypeId = headerReader.GetInt32(1);
		var templateName = headerReader.GetString(2);
		var scenarioCode = headerReader.IsDBNull(3) ? null : headerReader.GetString(3);
		var version = headerReader.GetInt32(4);
		var topPlate = headerReader.IsDBNull(5) ? null : headerReader.GetString(5);
		var intro = headerReader.IsDBNull(6) ? null : headerReader.GetString(6);
		var safety = headerReader.IsDBNull(7) ? null : headerReader.GetString(7);
		var red = headerReader.GetBoolean(8);
		var mtName = headerReader.GetString(9);
		var mtCode = headerReader.IsDBNull(10) ? null : headerReader.GetString(10);
		int? facilityId = headerReader.IsDBNull(11) ? null : Convert.ToInt32(headerReader.GetValue(11));
		await headerReader.CloseAsync().ConfigureAwait(false);

		var fields = await LoadFieldsAsync(connection, templateId, cancellationToken).ConfigureAwait(false);

		return new TemplateSyncPayload(
			Guid.NewGuid().ToString(),
			templateId,
			equipmentTypeId,
			maintenanceTypeId,
			facilityId,
			templateName,
			scenarioCode,
			version,
			topPlate,
			intro,
			safety,
			red,
			mtName,
			mtCode,
			fields);
	}

	private static async Task<IReadOnlyList<TemplateFieldSyncPayload>> LoadFieldsAsync(
		NpgsqlConnection connection,
		int templateId,
		CancellationToken cancellationToken)
	{
		var rows = new List<(int SortOrder, string? FieldCode, string QuestionText, string? HintText, string FieldTypeName, string? ValidationRuleCode, bool IsRequired, string? GroupName, long ItemId)>();
		await using (var cmd = new NpgsqlCommand("""
			SELECT cti.sort_order, cti.field_code, cti.question_text, cti.hint_text, ft.type_name,
			       cti.validation_rule_code, cti.is_required, cti.group_name, cti.id
			FROM checklist_template_items cti
			INNER JOIN field_types ft ON ft.id = cti.field_type_id
			WHERE cti.checklist_template_id = @tid
			ORDER BY cti.sort_order;
			""", connection))
		{
			cmd.Parameters.AddWithValue("tid", templateId);
			await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				rows.Add((
					reader.GetInt32(0),
					reader.IsDBNull(1) ? null : reader.GetString(1),
					reader.GetString(2),
					reader.IsDBNull(3) ? null : reader.GetString(3),
					reader.GetString(4),
					reader.IsDBNull(5) ? null : reader.GetString(5),
					reader.GetBoolean(6),
					reader.IsDBNull(7) ? null : reader.GetString(7),
					reader.GetInt64(8)));
			}
		}

		var list = new List<TemplateFieldSyncPayload>(rows.Count);
		foreach (var row in rows)
		{
			var options = await LoadOptionsAsync(connection, row.ItemId, cancellationToken).ConfigureAwait(false);
			list.Add(new TemplateFieldSyncPayload(
				row.SortOrder,
				row.FieldCode,
				row.QuestionText,
				row.HintText,
				row.FieldTypeName,
				row.ValidationRuleCode,
				row.IsRequired,
				row.GroupName,
				options));
		}

		return list;
	}

	private static async Task<IReadOnlyList<string>> LoadOptionsAsync(
		NpgsqlConnection connection,
		long itemId,
		CancellationToken cancellationToken)
	{
		var list = new List<string>();
		await using var cmd = new NpgsqlCommand("""
			SELECT option_label
			FROM checklist_template_item_options
			WHERE checklist_template_item_id = @iid
			ORDER BY sort_order;
			""", connection);
		cmd.Parameters.AddWithValue("iid", itemId);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			list.Add(reader.GetString(0));
		return list;
	}
}
