using System.Globalization;
using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteChecklistSaveService : IChecklistSaveService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteChecklistSaveService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<ChecklistSaveResult> SaveAsync(SaveChecklistRequest request, CancellationToken cancellationToken = default)
	{
		try
		{
			await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
			await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

			var items = await LoadItemRowsAsync(connection, request.ChecklistTemplateId, cancellationToken).ConfigureAwait(false);
			if (items.Count == 0)
				return new ChecklistSaveResult(false, null, "В шаблоне нет полей для сохранения.");

			var startAt = CombineDateTime(request.AnswersByTemplateItemId, items, "start_date", "start_time");
			var endAt = CombineDateTime(request.AnswersByTemplateItemId, items, "end_date", "end_time");

			var (status, syncState) = request.PersistenceMode switch
			{
				ChecklistPersistenceMode.LocalDraft => ("draft", "local"),
				ChecklistPersistenceMode.UploadQueue => ("completed", "pending_upload"),
				_ => ("draft", "local")
			};

			var checklistId = await InsertChecklistAsync(
				connection,
				tx,
				request.InstallationId,
				request.MaintenanceTypeId,
				request.EngineerUserId,
				request.ChecklistTemplateId,
				startAt,
				endAt,
				status,
				syncState,
				cancellationToken).ConfigureAwait(false);

			foreach (var item in items)
			{
				if (!request.AnswersByTemplateItemId.TryGetValue(item.Id, out var raw))
					raw = string.Empty;

				await InsertResponseAsync(connection, tx, checklistId, item, raw, cancellationToken).ConfigureAwait(false);
			}

			await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
			return new ChecklistSaveResult(true, checklistId, null);
		}
		catch (Exception ex)
		{
			return new ChecklistSaveResult(false, null, ex.Message);
		}
	}

	private static async Task<int> InsertChecklistAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		int installationId,
		int maintenanceTypeId,
		int engineerId,
		int templateId,
		DateTimeOffset? startAt,
		DateTimeOffset? endAt,
		string status,
		string syncState,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.Transaction = tx;
		cmd.CommandText = """
			INSERT INTO checklists (
				installation_id, maintenance_type_id, engineer_id, checklist_template_id,
				start_at, end_at, status, sync_state, local_updated_at)
			VALUES (
				$i, $m, $e, $t,
				$start, $end, $status, $sync, datetime('now'));
			SELECT last_insert_rowid();
			""";
		cmd.Parameters.AddWithValue("$i", installationId);
		cmd.Parameters.AddWithValue("$m", maintenanceTypeId);
		cmd.Parameters.AddWithValue("$e", engineerId);
		cmd.Parameters.AddWithValue("$t", templateId);
		cmd.Parameters.AddWithValue("$start", startAt.HasValue ? startAt.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
		cmd.Parameters.AddWithValue("$end", endAt.HasValue ? endAt.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
		cmd.Parameters.AddWithValue("$status", status);
		cmd.Parameters.AddWithValue("$sync", syncState);
		var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is long l ? (int)l : Convert.ToInt32(scalar ?? 0);
	}

	private static async Task InsertResponseAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		int checklistId,
		ItemRow item,
		string raw,
		CancellationToken cancellationToken)
	{
		var trimmed = raw?.Trim() ?? string.Empty;
		var isMulti = string.Equals(item.FieldTypeName, "dropdown_multiple", StringComparison.OrdinalIgnoreCase)
			|| (string.Equals(item.FieldTypeName, "checkbox", StringComparison.OrdinalIgnoreCase) && item.OptionCount > 1);

		if (isMulti)
		{
			var ids = ParseOptionIdList(trimmed);
			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
				VALUES ($c, $item, NULL);
				SELECT last_insert_rowid();
				""";
			cmd.Parameters.AddWithValue("$c", checklistId);
			cmd.Parameters.AddWithValue("$item", item.Id);
			var ridObj = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			var responseId = ridObj is long l ? (int)l : Convert.ToInt32(ridObj ?? 0);
			foreach (var oid in ids)
			{
				using var m = connection.CreateCommand();
				m.Transaction = tx;
				m.CommandText = """
					INSERT OR IGNORE INTO checklist_response_multi_options (checklist_response_id, checklist_template_item_option_id)
					VALUES ($r, $o);
					""";
				m.Parameters.AddWithValue("$r", responseId);
				m.Parameters.AddWithValue("$o", oid);
				await m.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}

			return;
		}

		if (string.Equals(item.FieldTypeName, "boolean", StringComparison.OrdinalIgnoreCase))
		{
			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT INTO checklist_responses (checklist_id, checklist_template_item_id, boolean_response)
				VALUES ($c, $item, $b);
				""";
			cmd.Parameters.AddWithValue("$c", checklistId);
			cmd.Parameters.AddWithValue("$item", item.Id);
			if (bool.TryParse(trimmed, out var b))
				cmd.Parameters.AddWithValue("$b", b ? 1 : 0);
			else
				cmd.Parameters.AddWithValue("$b", DBNull.Value);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			return;
		}

		if (string.Equals(item.FieldTypeName, "radio", StringComparison.OrdinalIgnoreCase)
		    || string.Equals(item.FieldTypeName, "dropdown", StringComparison.OrdinalIgnoreCase))
		{
			int? selected = int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var optId)
				? optId
				: null;

			using var cmd2 = connection.CreateCommand();
			cmd2.Transaction = tx;
			cmd2.CommandText = """
				INSERT INTO checklist_responses (checklist_id, checklist_template_item_id, selected_option_id, text_response)
				VALUES ($c, $item, $opt, $txt);
				""";
			cmd2.Parameters.AddWithValue("$c", checklistId);
			cmd2.Parameters.AddWithValue("$item", item.Id);
			cmd2.Parameters.AddWithValue("$opt", selected.HasValue ? selected.Value : DBNull.Value);
			cmd2.Parameters.AddWithValue("$txt", selected.HasValue ? DBNull.Value : trimmed.Length > 0 ? trimmed : DBNull.Value);
			await cmd2.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			return;
		}

		if (string.Equals(item.FieldTypeName, "number", StringComparison.OrdinalIgnoreCase)
		    && double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
		{
			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT INTO checklist_responses (checklist_id, checklist_template_item_id, numeric_response)
				VALUES ($c, $item, $n);
				""";
			cmd.Parameters.AddWithValue("$c", checklistId);
			cmd.Parameters.AddWithValue("$item", item.Id);
			cmd.Parameters.AddWithValue("$n", num);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			return;
		}

		using var cmdText = connection.CreateCommand();
		cmdText.Transaction = tx;
		cmdText.CommandText = """
			INSERT INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
			VALUES ($c, $item, $txt);
			""";
		cmdText.Parameters.AddWithValue("$c", checklistId);
		cmdText.Parameters.AddWithValue("$item", item.Id);
		cmdText.Parameters.AddWithValue("$txt", trimmed.Length > 0 ? trimmed : DBNull.Value);
		await cmdText.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static List<int> ParseOptionIdList(string trimmed)
	{
		if (trimmed.Length == 0)
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

	private static DateTimeOffset? CombineDateTime(
		IReadOnlyDictionary<int, string> answers,
		IReadOnlyList<ItemRow> items,
		string dateCode,
		string timeCode)
	{
		var dateItem = items.FirstOrDefault(i => string.Equals(i.FieldCode, dateCode, StringComparison.OrdinalIgnoreCase));
		if (dateItem is null)
			return null;
		if (!answers.TryGetValue(dateItem.Id, out var dateRaw) || string.IsNullOrWhiteSpace(dateRaw))
			return null;

		var timeRaw = "";
		var timeItem = items.FirstOrDefault(i => string.Equals(i.FieldCode, timeCode, StringComparison.OrdinalIgnoreCase));
		if (timeItem is not null && answers.TryGetValue(timeItem.Id, out var tr) && tr is not null)
			timeRaw = tr;

		if (!DateOnly.TryParse(dateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
			return null;

		var time = TimeSpan.Zero;
		if (!string.IsNullOrWhiteSpace(timeRaw) && TimeOnly.TryParse(timeRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
			time = t.ToTimeSpan();

		var local = d.ToDateTime(TimeOnly.MinValue).Add(time);
		var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Local);
		return new DateTimeOffset(unspecified);
	}

	private static async Task<List<ItemRow>> LoadItemRowsAsync(
		SqliteConnection connection,
		int templateId,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT cti.id, cti.field_code, ft.type_name,
				(SELECT COUNT(*) FROM checklist_template_item_options o WHERE o.checklist_template_item_id = cti.id)
			FROM checklist_template_items cti
			INNER JOIN field_types ft ON ft.id = cti.field_type_id
			WHERE cti.checklist_template_id = $tid
			ORDER BY cti.sort_order;
			""";
		cmd.Parameters.AddWithValue("$tid", templateId);

		var list = new List<ItemRow>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			list.Add(new ItemRow(
				reader.GetInt32(0),
				reader.IsDBNull(1) ? null : reader.GetString(1),
				reader.GetString(2),
				reader.GetInt32(3)));
		}

		return list;
	}

	private sealed record ItemRow(int Id, string? FieldCode, string FieldTypeName, int OptionCount);
}
