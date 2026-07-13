using MRS.Application.Sync;
using Npgsql;

namespace MRS.Infrastructure.Postgres;

internal static class PostgresSyncChecklistReader
{
	internal static async Task<IReadOnlyList<SyncChecklistPullRow>> ReadAllAsync(
		NpgsqlConnection connection,
		CancellationToken cancellationToken)
	{
		var ids = new List<long>();
		await using (var cmd = new NpgsqlCommand("""
			SELECT id FROM checklists
			WHERE is_active = TRUE AND client_uuid IS NOT NULL
			ORDER BY id;
			""", connection))
		{
			await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				ids.Add(reader.GetInt64(0));
		}

		var list = new List<SyncChecklistPullRow>(ids.Count);
		foreach (var id in ids)
			list.Add(await ReadOneAsync(connection, id, cancellationToken).ConfigureAwait(false));
		return list;
	}

	private static async Task<SyncChecklistPullRow> ReadOneAsync(
		NpgsqlConnection connection,
		long checklistId,
		CancellationToken cancellationToken)
	{
		Guid clientUuid;
		int installationId;
		int maintenanceTypeId;
		int? templateId;
		int engineerId;
		DateTimeOffset? startAt;
		DateTimeOffset? endAt;
		string status;
		DateTimeOffset serverUpdatedAt;

		await using (var cmd = new NpgsqlCommand("""
			SELECT client_uuid, installation_id, maintenance_type_id, checklist_template_id, engineer_id,
			       start_at, end_at, status, COALESCE(server_updated_at, local_updated_at, NOW())
			FROM checklists
			WHERE id = @id;
			""", connection))
		{
			cmd.Parameters.AddWithValue("id", checklistId);
			await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				throw new InvalidOperationException($"Контрольный лист {checklistId} не найден.");

			clientUuid = reader.GetGuid(0);
			installationId = reader.GetInt32(1);
			maintenanceTypeId = reader.GetInt32(2);
			templateId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
			engineerId = reader.GetInt32(4);
			startAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
			endAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6);
			status = reader.GetString(7);
			serverUpdatedAt = reader.GetDateTime(8);
		}

		var responses = await LoadResponsesAsync(connection, checklistId, cancellationToken).ConfigureAwait(false);
		var payload = new ChecklistSyncPayload(
			clientUuid.ToString(),
			(int)checklistId,
			installationId,
			maintenanceTypeId,
			templateId,
			engineerId,
			startAt,
			endAt,
			status,
			responses);

		return new SyncChecklistPullRow(payload, serverUpdatedAt);
	}

	private static async Task<IReadOnlyList<ChecklistResponseSyncPayload>> LoadResponsesAsync(
		NpgsqlConnection connection,
		long checklistId,
		CancellationToken cancellationToken)
	{
		var rows = new List<(long Id, int TemplateItemId, string? Text, double? Num, bool? Bool, int? Opt)>();
		await using (var cmd = new NpgsqlCommand("""
			SELECT id, checklist_template_item_id, text_response, numeric_response, boolean_response, selected_option_id
			FROM checklist_responses
			WHERE checklist_id = @id;
			""", connection))
		{
			cmd.Parameters.AddWithValue("id", checklistId);
			await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				rows.Add((
					reader.GetInt64(0),
					reader.GetInt32(1),
					reader.IsDBNull(2) ? null : reader.GetString(2),
					reader.IsDBNull(3) ? null : reader.GetDouble(3),
					reader.IsDBNull(4) ? null : reader.GetBoolean(4),
					reader.IsDBNull(5) ? null : reader.GetInt32(5)));
			}
		}

		var list = new List<ChecklistResponseSyncPayload>(rows.Count);
		foreach (var row in rows)
		{
			var multi = await LoadMultiOptionsAsync(connection, row.Id, cancellationToken).ConfigureAwait(false);
			list.Add(new ChecklistResponseSyncPayload(
				row.TemplateItemId,
				row.Text,
				row.Num,
				row.Bool,
				row.Opt,
				multi.Count > 0 ? multi : null));
		}

		return list;
	}

	private static async Task<IReadOnlyList<int>> LoadMultiOptionsAsync(
		NpgsqlConnection connection,
		long responseId,
		CancellationToken cancellationToken)
	{
		var list = new List<int>();
		await using var cmd = new NpgsqlCommand("""
			SELECT checklist_template_item_option_id
			FROM checklist_response_multi_options
			WHERE checklist_response_id = @id;
			""", connection);
		cmd.Parameters.AddWithValue("id", responseId);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			list.Add(reader.GetInt32(0));
		return list;
	}
}
