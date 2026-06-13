using System.Globalization;
using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteChecklistEditService : IChecklistEditService
{
	private static readonly HashSet<string> LockedFieldCodes = new(StringComparer.OrdinalIgnoreCase)
	{
		"unit_number",
		"equipment_pick",
		"start_time",
		"end_time"
	};

	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteChecklistEditService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<ChecklistEditModel> GetForEditAsync(int checklistId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);

		using var infoCmd = connection.CreateCommand();
		infoCmd.CommandText = """
			SELECT
				c.id,
				i.id AS installation_id,
				c.start_at,
				c.end_at,
				c.status,
				COALESCE(o.short_name, o.full_name) AS organization_name,
				f.name AS facility_name,
				et.type_name AS equipment_type_name,
				COALESCE(NULLIF(TRIM(i.custom_name), ''), CAST(i.id AS TEXT)) AS installation_label,
				mt.type_name AS maintenance_type_name,
				i.equipment_type_id AS equipment_type_id,
				c.maintenance_type_id AS maintenance_type_id,
				ct.id AS checklist_template_id
			FROM checklists c
			INNER JOIN installations i ON i.id = c.installation_id
			INNER JOIN equipment_types et ON et.id = i.equipment_type_id
			INNER JOIN facility_systems fs ON fs.id = i.system_id
			INNER JOIN facilities f ON f.id = fs.facility_id
			INNER JOIN organizations o ON o.id = f.organization_id
			INNER JOIN maintenance_types mt ON mt.id = c.maintenance_type_id
			LEFT JOIN checklist_templates ct ON ct.id = c.checklist_template_id
			WHERE c.id = $cid AND c.is_active = 1;
			""";
		infoCmd.Parameters.AddWithValue("$cid", checklistId);

		DateTimeOffset? startAt = null;
		DateTimeOffset? endAt = null;

		await using var infoReader = await infoCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await infoReader.ReadAsync(cancellationToken).ConfigureAwait(false))
			throw new InvalidOperationException("Контрольный лист не найден (или неактивен).");

		var id = infoReader.GetInt32(0);
		var installationId = infoReader.GetInt32(1);
		if (!infoReader.IsDBNull(2) && DateTimeOffset.TryParse(infoReader.GetString(2), out var s))
			startAt = s;
		if (!infoReader.IsDBNull(3) && DateTimeOffset.TryParse(infoReader.GetString(3), out var e))
			endAt = e;

		var statusCode = infoReader.GetString(4);
		var org = infoReader.GetString(5);
		var facility = infoReader.GetString(6);
		var equipmentType = infoReader.GetString(7);
		var installationLabel = infoReader.GetString(8);
		var maintenanceType = infoReader.GetString(9);
		int? templateId = infoReader.IsDBNull(12) ? null : infoReader.GetInt32(12);

		// demo-данные могут хранить checklist_template_id = NULL.
		// Тогда находим актуальный шаблон по (equipment_type_id, maintenance_type_id).
		if (templateId is null)
		{
			var equipmentTypeId = infoReader.GetInt32(10);
			var maintenanceTypeId = infoReader.GetInt32(11);

			using var resolve = connection.CreateCommand();
			resolve.CommandText = """
				SELECT id
				FROM checklist_templates
				WHERE equipment_type_id = $et AND maintenance_type_id = $mt AND is_active = 1
				ORDER BY version DESC
				LIMIT 1;
				""";
			resolve.Parameters.AddWithValue("$et", equipmentTypeId);
			resolve.Parameters.AddWithValue("$mt", maintenanceTypeId);
			var scalar = await resolve.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			templateId = scalar is long l ? (int)l : (scalar is null ? null : Convert.ToInt32(scalar, CultureInfo.InvariantCulture));
		}

		if (templateId is null)
			throw new InvalidOperationException("Не удалось определить шаблон контрольного листа.");

		var fields = new List<ChecklistEditField>();
		using var fieldsCmd = connection.CreateCommand();
		fieldsCmd.CommandText = """
			SELECT
				cti.id,
				cti.field_code,
				cti.question_text,
				cti.hint_text,
				ft.type_name,
				cti.sort_order
			FROM checklist_template_items cti
			INNER JOIN field_types ft ON ft.id = cti.field_type_id
			WHERE cti.checklist_template_id = $tid
			ORDER BY cti.sort_order;
			""";
		fieldsCmd.Parameters.AddWithValue("$tid", templateId.Value);

		// Preload responses for performance
		using var respCmd = connection.CreateCommand();
		respCmd.CommandText = """
			SELECT
				cr.checklist_template_item_id,
				cr.id,
				cr.boolean_response,
				cr.text_response,
				cr.numeric_response,
				cr.selected_option_id
			FROM checklist_responses cr
			WHERE cr.checklist_id = $cid;
			""";
		respCmd.Parameters.AddWithValue("$cid", checklistId);

		var respRows = new Dictionary<int, (int responseId, int? boolResp, string? textResp, double? numResp, int? selectedOptId)>();
		await using (var respReader = await respCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
		{
			while (await respReader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				var itemId = respReader.GetInt32(0);
				var responseId = respReader.GetInt32(1);
				int? b = respReader.IsDBNull(2) ? null : respReader.GetInt32(2);
				string? t = respReader.IsDBNull(3) ? null : respReader.GetString(3);
				double? n = respReader.IsDBNull(4) ? null : respReader.GetDouble(4);
				int? sopt = respReader.IsDBNull(5) ? null : respReader.GetInt32(5);
				respRows[itemId] = (responseId, b, t, n, sopt);
			}
		}

		await using var itemReader = await fieldsCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await itemReader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var templateItemId = itemReader.GetInt32(0);
			var fieldCode = itemReader.IsDBNull(1) ? null : itemReader.GetString(1);
			var question = itemReader.GetString(2);
			var hint = itemReader.IsDBNull(3) ? null : itemReader.GetString(3);
			var fieldType = itemReader.GetString(4);

			var isLocked = fieldCode is not null && LockedFieldCodes.Contains(fieldCode);

			var options = await LoadOptionsAsync(connection, templateItemId, cancellationToken).ConfigureAwait(false);

			// compute ValueRaw based on existing checklist_responses
			string valueRaw = string.Empty;
			if (respRows.TryGetValue(templateItemId, out var row))
			{
				if (string.Equals(fieldType, "boolean", StringComparison.OrdinalIgnoreCase))
				{
					valueRaw = row.boolResp.HasValue ? (row.boolResp.Value == 1 ? "true" : "false") : string.Empty;
				}
				else if (string.Equals(fieldType, "number", StringComparison.OrdinalIgnoreCase))
				{
					valueRaw = row.numResp.HasValue ? row.numResp.Value.ToString("G", CultureInfo.InvariantCulture) : string.Empty;
				}
				else if (string.Equals(fieldType, "radio", StringComparison.OrdinalIgnoreCase) ||
				         string.Equals(fieldType, "dropdown", StringComparison.OrdinalIgnoreCase))
				{
					valueRaw = row.selectedOptId.HasValue ? row.selectedOptId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
				}
				else if (string.Equals(fieldType, "dropdown_multiple", StringComparison.OrdinalIgnoreCase) ||
				         string.Equals(fieldType, "checkbox", StringComparison.OrdinalIgnoreCase))
				{
					var multiOptionIds = await LoadMultiOptionsAsync(connection, row.responseId, cancellationToken).ConfigureAwait(false);
					valueRaw = multiOptionIds.Count == 0 ? string.Empty : string.Join(",", multiOptionIds);
				}
				else
				{
					valueRaw = row.textResp ?? string.Empty;
				}
			}

			// Time auto: start/end dates/times are treated as derived from checklist columns.
			if (fieldCode is not null)
			{
				if (fieldCode.Equals("start_date", StringComparison.OrdinalIgnoreCase))
					valueRaw = startAt?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
				else if (fieldCode.Equals("end_date", StringComparison.OrdinalIgnoreCase))
					valueRaw = endAt?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
				else if (fieldCode.Equals("start_time", StringComparison.OrdinalIgnoreCase))
					valueRaw = startAt?.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
				else if (fieldCode.Equals("end_time", StringComparison.OrdinalIgnoreCase))
					valueRaw = endAt?.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
			}

			fields.Add(new ChecklistEditField(
				templateItemId,
				fieldCode,
				question,
				hint,
				fieldType,
				isLocked,
				options,
				valueRaw));
		}

		var info = new ChecklistEditInfo(
			id,
			installationId,
			startAt,
			endAt,
			org,
			facility,
			equipmentType,
			installationLabel,
			maintenanceType,
			statusCode,
			templateId.Value);

		return new ChecklistEditModel(info, fields);
	}

	public async Task SetStatusAsync(int checklistId, string status, string syncState, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			UPDATE checklists
			SET status = $status,
			    sync_state = $sync,
			    local_updated_at = datetime('now')
			WHERE id = $id AND is_active = 1;
			""";
		cmd.Parameters.AddWithValue("$id", checklistId);
		cmd.Parameters.AddWithValue("$status", status);
		cmd.Parameters.AddWithValue("$sync", syncState);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task<ChecklistUpdateDryRunResult> ValidateAsync(UpdateChecklistAnswersRequest request, CancellationToken cancellationToken = default)
	{
		var model = await GetForEditAsync(request.ChecklistId, cancellationToken).ConfigureAwait(false);

		// Editable = all template items except those locked by fork-dependent selections.
		var editable = model.Fields.Where(f => !f.IsLocked).ToList();

		var canSave = new List<ChecklistUpdateDryRunField>();
		var cannotSave = new List<ChecklistUpdateDryRunField>();

		foreach (var field in editable)
		{
			var raw = request.AnswersByTemplateItemId.TryGetValue(field.TemplateItemId, out var v) ? v : field.ValueRaw;
			try
			{
				await TryApplySingleInternalAsync(request.ChecklistId, model.Info.ChecklistTemplateId, field, raw, dryRun: true, cancellationToken)
					.ConfigureAwait(false);
				canSave.Add(new ChecklistUpdateDryRunField(field.TemplateItemId, field.QuestionText));
			}
			catch
			{
				cannotSave.Add(new ChecklistUpdateDryRunField(field.TemplateItemId, field.QuestionText));
			}
		}

		return new ChecklistUpdateDryRunResult(
			cannotSave.Count == 0,
			canSave,
			cannotSave);
	}

	public async Task<ChecklistUpdateApplyResult> ApplyAsync(
		UpdateChecklistAnswersRequest request,
		IReadOnlyCollection<int> templateItemIdsToApply,
		CancellationToken cancellationToken = default)
	{
		var model = await GetForEditAsync(request.ChecklistId, cancellationToken).ConfigureAwait(false);

		var set = templateItemIdsToApply is null ? new HashSet<int>() : templateItemIdsToApply.ToHashSet();
		if (set.Count == 0)
			return new ChecklistUpdateApplyResult(true, null);

		var editableByRule = model.Fields.Where(f => !f.IsLocked).ToDictionary(f => f.TemplateItemId, f => f);
		var toApply = set.Where(id => editableByRule.ContainsKey(id)).Select(id => editableByRule[id]).ToList();

		if (toApply.Count == 0)
			return new ChecklistUpdateApplyResult(true, null);

		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			foreach (var field in toApply.OrderBy(f => f.TemplateItemId))
			{
				var raw = request.AnswersByTemplateItemId.TryGetValue(field.TemplateItemId, out var v) ? v : field.ValueRaw;
				await UpsertResponseForItemAsync(connection, tx, request.ChecklistId, field, raw, cancellationToken).ConfigureAwait(false);
				await UpdateChecklistDatesIfNeededAsync(connection, tx, request.ChecklistId, field, raw, cancellationToken).ConfigureAwait(false);
			}

			await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
			return new ChecklistUpdateApplyResult(true, null);
		}
		catch (Exception ex)
		{
			await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
			return new ChecklistUpdateApplyResult(false, ex.Message);
		}
	}

	private async Task TryApplySingleInternalAsync(
		int checklistId,
		int checklistTemplateId,
		ChecklistEditField field,
		string raw,
		bool dryRun,
		CancellationToken cancellationToken)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			await UpsertResponseForItemAsync(connection, tx, checklistId, field, raw, cancellationToken).ConfigureAwait(false);
			await UpdateChecklistDatesIfNeededAsync(connection, tx, checklistId, field, raw, cancellationToken).ConfigureAwait(false);
			if (dryRun)
			{
				await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch
		{
			try { await tx.RollbackAsync(cancellationToken).ConfigureAwait(false); } catch { /* ignore */ }
			throw;
		}
	}

	private static async Task<List<int>> LoadMultiOptionsAsync(
		SqliteConnection connection,
		int checklistResponseId,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT checklist_template_item_option_id
			FROM checklist_response_multi_options
			WHERE checklist_response_id = $rid
			ORDER BY checklist_template_item_option_id;
			""";
		cmd.Parameters.AddWithValue("$rid", checklistResponseId);

		var list = new List<int>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			list.Add(reader.GetInt32(0));
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

		var list = new List<TemplateFieldOption>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			list.Add(new TemplateFieldOption(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));
		return list;
	}

	private static async Task UpsertResponseForItemAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		int checklistId,
		ChecklistEditField field,
		string raw,
		CancellationToken cancellationToken)
	{
		var templateItemId = field.TemplateItemId;
		var fieldType = field.FieldTypeName;
		var fieldCode = field.FieldCode;

		int? responseId = null;
		using (var find = connection.CreateCommand())
		{
			find.Transaction = tx;
			find.CommandText = """
				SELECT id
				FROM checklist_responses
				WHERE checklist_id = $cid AND checklist_template_item_id = $iid;
				""";
			find.Parameters.AddWithValue("$cid", checklistId);
			find.Parameters.AddWithValue("$iid", templateItemId);
			var scalar = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (scalar is long l)
				responseId = (int)l;
			else if (scalar is not null)
				responseId = Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
		}

		// normalize raw
		var trimmed = raw?.Trim() ?? string.Empty;
		var isMulti = string.Equals(fieldType, "dropdown_multiple", StringComparison.OrdinalIgnoreCase) ||
		              (string.Equals(fieldType, "checkbox", StringComparison.OrdinalIgnoreCase) && field.Options.Count > 1);

		if (responseId is null)
		{
			using var ins = connection.CreateCommand();
			ins.Transaction = tx;
			ins.CommandText = """
				INSERT INTO checklist_responses (checklist_id, checklist_template_item_id, boolean_response, text_response, numeric_response, selected_option_id)
				VALUES ($cid, $iid, NULL, NULL, NULL, NULL);
				SELECT last_insert_rowid();
				""";
			ins.Parameters.AddWithValue("$cid", checklistId);
			ins.Parameters.AddWithValue("$iid", templateItemId);
			var scalar = await ins.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			responseId = scalar is long l2 ? (int)l2 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
		}

		// clear multi-options if needed
		if (isMulti)
		{
			using var upd = connection.CreateCommand();
			upd.Transaction = tx;
			upd.CommandText = """
				UPDATE checklist_responses
				SET boolean_response = NULL, text_response = NULL, numeric_response = NULL, selected_option_id = NULL
				WHERE id = $rid;
				""";
			upd.Parameters.AddWithValue("$rid", responseId.Value);
			await upd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

			using (var del = connection.CreateCommand())
			{
				del.Transaction = tx;
				del.CommandText = """
					DELETE FROM checklist_response_multi_options
					WHERE checklist_response_id = $rid;
					""";
				del.Parameters.AddWithValue("$rid", responseId.Value);
				await del.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}

			var ids = ParseOptionIds(trimmed);
			foreach (var oid in ids)
			{
				using var m = connection.CreateCommand();
				m.Transaction = tx;
				m.CommandText = """
					INSERT OR IGNORE INTO checklist_response_multi_options (checklist_response_id, checklist_template_item_option_id)
					VALUES ($rid, $opt);
					""";
				m.Parameters.AddWithValue("$rid", responseId.Value);
				m.Parameters.AddWithValue("$opt", oid);
				await m.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}

			return;
		}

		if (string.Equals(fieldType, "boolean", StringComparison.OrdinalIgnoreCase))
		{
			int? b = null;
			if (!string.IsNullOrWhiteSpace(trimmed) && bool.TryParse(trimmed, out var bv))
				b = bv ? 1 : 0;

			using var upd = connection.CreateCommand();
			upd.Transaction = tx;
			upd.CommandText = """
				UPDATE checklist_responses
				SET boolean_response = $b, text_response = NULL, numeric_response = NULL, selected_option_id = NULL
				WHERE id = $rid;
				""";
			upd.Parameters.AddWithValue("$rid", responseId.Value);
			upd.Parameters.AddWithValue("$b", b.HasValue ? b.Value : DBNull.Value);
			await upd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			return;
		}

		if (string.Equals(fieldType, "radio", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(fieldType, "dropdown", StringComparison.OrdinalIgnoreCase))
		{
			int? selected = null;
			if (!string.IsNullOrWhiteSpace(trimmed) && int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var optId))
				selected = optId;

			using var upd = connection.CreateCommand();
			upd.Transaction = tx;
			upd.CommandText = """
				UPDATE checklist_responses
				SET selected_option_id = $sel, boolean_response = NULL, text_response = NULL, numeric_response = NULL
				WHERE id = $rid;
				""";
			upd.Parameters.AddWithValue("$rid", responseId.Value);
			upd.Parameters.AddWithValue("$sel", selected.HasValue ? selected.Value : DBNull.Value);
			await upd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			return;
		}

		if (string.Equals(fieldType, "number", StringComparison.OrdinalIgnoreCase))
		{
			double? n = null;
			if (!string.IsNullOrWhiteSpace(trimmed) && double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv))
				n = dv;

			using var upd = connection.CreateCommand();
			upd.Transaction = tx;
			upd.CommandText = """
				UPDATE checklist_responses
				SET numeric_response = $n, boolean_response = NULL, text_response = NULL, selected_option_id = NULL
				WHERE id = $rid;
				""";
			upd.Parameters.AddWithValue("$rid", responseId.Value);
			upd.Parameters.AddWithValue("$n", n.HasValue ? n.Value : DBNull.Value);
			await upd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			return;
		}

		// text-like (text/textarea/date/time/datetime)
		using var updText = connection.CreateCommand();
		updText.Transaction = tx;
		updText.CommandText = """
			UPDATE checklist_responses
			SET text_response = $txt, boolean_response = NULL, numeric_response = NULL, selected_option_id = NULL
			WHERE id = $rid;
			""";
		updText.Parameters.AddWithValue("$rid", responseId.Value);
		updText.Parameters.AddWithValue("$txt", trimmed.Length == 0 ? DBNull.Value : trimmed);
		await updText.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task UpdateChecklistDatesIfNeededAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		int checklistId,
		ChecklistEditField field,
		string raw,
		CancellationToken cancellationToken)
	{
		if (field.FieldCode is null)
			return;

		var now = DateTimeOffset.Now;
		if (field.FieldCode.Equals("start_date", StringComparison.OrdinalIgnoreCase))
		{
			var date = ParseDateOnly(raw);
			if (date is null)
				throw new InvalidOperationException("Некорректная дата начала.");

			var local = new DateTime(date.Value.Year, date.Value.Month, date.Value.Day, now.Hour, now.Minute, 0, DateTimeKind.Local);
			var dto = new DateTimeOffset(local);

			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				UPDATE checklists
				SET start_at = $s, local_updated_at = datetime('now')
				WHERE id = $cid;
				""";
			cmd.Parameters.AddWithValue("$cid", checklistId);
			cmd.Parameters.AddWithValue("$s", dto.ToString("O", CultureInfo.InvariantCulture));
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			return;
		}

		if (field.FieldCode.Equals("end_date", StringComparison.OrdinalIgnoreCase))
		{
			var date = ParseDateOnly(raw);
			if (date is null)
				throw new InvalidOperationException("Некорректная дата окончания.");

			var local = new DateTime(date.Value.Year, date.Value.Month, date.Value.Day, now.Hour, now.Minute, 0, DateTimeKind.Local);
			var dto = new DateTimeOffset(local);

			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				UPDATE checklists
				SET end_at = $e, local_updated_at = datetime('now')
				WHERE id = $cid;
				""";
			cmd.Parameters.AddWithValue("$cid", checklistId);
			cmd.Parameters.AddWithValue("$e", dto.ToString("O", CultureInfo.InvariantCulture));
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private static DateOnly? ParseDateOnly(string raw)
	{
		var trimmed = raw?.Trim() ?? string.Empty;
		if (trimmed.Length == 0)
			return null;

		if (DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
			return d;

		return null;
	}

	private static List<int> ParseOptionIds(string trimmed)
	{
		if (string.IsNullOrWhiteSpace(trimmed))
			return [];
		var parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var list = new List<int>();
		foreach (var p in parts)
		{
			if (int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
				list.Add(id);
		}
		return list;
	}
}

