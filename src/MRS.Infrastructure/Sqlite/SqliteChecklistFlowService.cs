using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteChecklistFlowService : IChecklistFlowService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteChecklistFlowService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<IReadOnlyList<MaintenanceForkOption>> GetMaintenanceForkAsync(int equipmentTypeId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT mt.id, mt.type_name, mt.code, ct.id, ct.template_name, ct.scenario_code
			FROM checklist_templates ct
			INNER JOIN maintenance_types mt ON mt.id = ct.maintenance_type_id
			WHERE ct.equipment_type_id = $et AND ct.is_active = 1
			ORDER BY
				CASE WHEN mt.code = 'INT-UNIFIED' THEN 1 ELSE 0 END,
				mt.id;
			""";
		cmd.Parameters.AddWithValue("$et", equipmentTypeId);

		var list = new List<MaintenanceForkOption>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			list.Add(new MaintenanceForkOption(
				reader.GetInt32(0),
				reader.GetString(1),
				reader.IsDBNull(2) ? null : reader.GetString(2),
				reader.GetInt32(3),
				reader.GetString(4),
				reader.IsDBNull(5) ? null : reader.GetString(5)));
		}

		return list;
	}

	public async Task<int?> ResolveTemplateIdAsync(int equipmentTypeId, int maintenanceTypeId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT id FROM checklist_templates
			WHERE equipment_type_id = $e AND maintenance_type_id = $m AND is_active = 1
			ORDER BY version DESC
			LIMIT 1;
			""";
		cmd.Parameters.AddWithValue("$e", equipmentTypeId);
		cmd.Parameters.AddWithValue("$m", maintenanceTypeId);
		var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		if (scalar is null)
			return null;
		return scalar is long l ? (int)l : Convert.ToInt32(scalar);
	}

	public async Task<ChecklistTemplateMeta?> GetTemplateMetaAsync(int checklistTemplateId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT template_name, top_plate_text, intro_modal_text, safety_modal_text, red_button_enabled
			FROM checklist_templates
			WHERE id = $id AND is_active = 1;
			""";
		cmd.Parameters.AddWithValue("$id", checklistTemplateId);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			return null;

		var red = reader.GetInt32(4) != 0;
		return new ChecklistTemplateMeta(
			reader.GetString(0),
			reader.IsDBNull(1) ? null : reader.GetString(1),
			reader.IsDBNull(2) ? null : reader.GetString(2),
			reader.IsDBNull(3) ? null : reader.GetString(3),
			red);
	}

	public async Task<IReadOnlyList<TemplateFieldDefinition>> GetTemplateFieldsAsync(int checklistTemplateId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT cti.id, cti.sort_order, cti.field_code, cti.question_text, cti.hint_text, ft.type_name, cti.is_required
			FROM checklist_template_items cti
			INNER JOIN field_types ft ON ft.id = cti.field_type_id
			WHERE cti.checklist_template_id = $tid
			ORDER BY cti.sort_order;
			""";
		cmd.Parameters.AddWithValue("$tid", checklistTemplateId);

		var rows = new List<(int Id, int Sort, string? FieldCode, string Question, string? Hint, string FieldType, bool Required)>();
		await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
		{
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				rows.Add((
					reader.GetInt32(0),
					reader.GetInt32(1),
					reader.IsDBNull(2) ? null : reader.GetString(2),
					reader.GetString(3),
					reader.IsDBNull(4) ? null : reader.GetString(4),
					reader.GetString(5),
					reader.GetInt32(6) != 0));
			}
		}

		var list = new List<TemplateFieldDefinition>(rows.Count);
		foreach (var r in rows)
		{
			var options = await LoadOptionsAsync(connection, r.Id, cancellationToken).ConfigureAwait(false);
			list.Add(new TemplateFieldDefinition(r.Id, r.Sort, r.FieldCode, r.Question, r.Hint, r.FieldType, r.Required, options));
		}

		return list;
	}

	private static async Task<IReadOnlyList<TemplateFieldOption>> LoadOptionsAsync(
		SqliteConnection connection,
		int templateItemId,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT id, option_label, sort_order
			FROM checklist_template_item_options
			WHERE checklist_template_item_id = $iid
			ORDER BY sort_order;
			""";
		cmd.Parameters.AddWithValue("$iid", templateItemId);
		var opts = new List<TemplateFieldOption>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			opts.Add(new TemplateFieldOption(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));
		return opts;
	}
}
