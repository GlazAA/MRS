using System.Text.Json;
using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

internal static class SqliteTemplateSyncPayloadBuilder
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	internal static async Task<string> BuildAsync(
		ILocalDatabasePath paths,
		ILocalDatabaseBootstrapper bootstrapper,
		int templateId,
		CancellationToken cancellationToken)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(paths, bootstrapper, cancellationToken)
			.ConfigureAwait(false);

		using var headerCmd = connection.CreateCommand();
		headerCmd.CommandText = """
			SELECT ct.equipment_type_id, ct.maintenance_type_id, ct.template_name, ct.scenario_code, ct.version,
			       ct.top_plate_text, ct.intro_modal_text, ct.safety_modal_text, ct.red_button_enabled,
			       mt.type_name, mt.code
			FROM checklist_templates ct
			INNER JOIN maintenance_types mt ON mt.id = ct.maintenance_type_id
			WHERE ct.id = $id;
			""";
		headerCmd.Parameters.AddWithValue("$id", templateId);
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
		var red = headerReader.GetInt32(8) != 0;
		var mtName = headerReader.GetString(9);
		var mtCode = headerReader.IsDBNull(10) ? null : headerReader.GetString(10);
		await headerReader.CloseAsync().ConfigureAwait(false);

		var fields = await LoadFieldsAsync(connection, templateId, cancellationToken).ConfigureAwait(false);

		var payload = new TemplateSyncPayload(
			Guid.NewGuid().ToString(),
			templateId,
			equipmentTypeId,
			maintenanceTypeId,
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

		return JsonSerializer.Serialize(payload, JsonOptions);
	}

	private static async Task<IReadOnlyList<TemplateFieldSyncPayload>> LoadFieldsAsync(
		SqliteConnection connection,
		int templateId,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT cti.sort_order, cti.field_code, cti.question_text, cti.hint_text, ft.type_name,
			       cti.validation_rule_code, cti.is_required, cti.group_name, cti.id
			FROM checklist_template_items cti
			INNER JOIN field_types ft ON ft.id = cti.field_type_id
			WHERE cti.checklist_template_id = $tid
			ORDER BY cti.sort_order;
			""";
		cmd.Parameters.AddWithValue("$tid", templateId);

		var list = new List<TemplateFieldSyncPayload>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var itemId = reader.GetInt32(8);
			var options = await LoadOptionsAsync(connection, itemId, cancellationToken).ConfigureAwait(false);
			list.Add(new TemplateFieldSyncPayload(
				reader.GetInt32(0),
				reader.IsDBNull(1) ? null : reader.GetString(1),
				reader.GetString(2),
				reader.IsDBNull(3) ? null : reader.GetString(3),
				reader.GetString(4),
				reader.IsDBNull(5) ? null : reader.GetString(5),
				reader.GetInt32(6) != 0,
				reader.IsDBNull(7) ? null : reader.GetString(7),
				options));
		}

		return list;
	}

	private static async Task<IReadOnlyList<string>> LoadOptionsAsync(
		SqliteConnection connection,
		int itemId,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT option_label
			FROM checklist_template_item_options
			WHERE checklist_template_item_id = $iid
			ORDER BY sort_order;
			""";
		cmd.Parameters.AddWithValue("$iid", itemId);
		var list = new List<string>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			list.Add(reader.GetString(0));
		return list;
	}
}
