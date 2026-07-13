using System.Globalization;
using Microsoft.Data.Sqlite;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

internal static class SqliteChecklistSyncApplier
{
	internal static async Task UpsertAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		SyncChecklistPullRow row,
		CancellationToken cancellationToken)
	{
		var payload = row.Payload;
		if (await SqliteSyncMergeGuard.ShouldSkipUuidEntityAsync(
			connection, tx, "checklists", "checklist", payload.ClientUuid, payload.LocalId, row.ServerUpdatedAt, cancellationToken).ConfigureAwait(false))
			return;

		var targetId = await ResolveChecklistIdAsync(connection, tx, payload.ClientUuid, payload.LocalId, cancellationToken)
			.ConfigureAwait(false);

		using var cmd = connection.CreateCommand();
		cmd.Transaction = tx;
		cmd.CommandText = """
			INSERT INTO checklists (
			    id, installation_id, maintenance_type_id, checklist_template_id, engineer_id,
			    start_at, end_at, status, is_active, client_uuid, sync_state, server_updated_at, local_updated_at)
			VALUES (
			    $id, $inst, $mt, $tpl, $eng,
			    $start, $end, $status, 1, $uuid, 'synced', $serverUpdated, datetime('now'))
			ON CONFLICT(id) DO UPDATE SET
			    installation_id = excluded.installation_id,
			    maintenance_type_id = excluded.maintenance_type_id,
			    checklist_template_id = excluded.checklist_template_id,
			    engineer_id = excluded.engineer_id,
			    start_at = excluded.start_at,
			    end_at = excluded.end_at,
			    status = excluded.status,
			    client_uuid = excluded.client_uuid,
			    sync_state = 'synced',
			    server_updated_at = excluded.server_updated_at,
			    local_updated_at = datetime('now');
			""";
		cmd.Parameters.AddWithValue("$id", targetId);
		cmd.Parameters.AddWithValue("$inst", payload.InstallationId);
		cmd.Parameters.AddWithValue("$mt", payload.MaintenanceTypeId);
		cmd.Parameters.AddWithValue("$tpl", payload.ChecklistTemplateId ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$eng", payload.EngineerId);
		cmd.Parameters.AddWithValue("$start", payload.StartAt.HasValue ? payload.StartAt.Value.ToString("O") : DBNull.Value);
		cmd.Parameters.AddWithValue("$end", payload.EndAt.HasValue ? payload.EndAt.Value.ToString("O") : DBNull.Value);
		cmd.Parameters.AddWithValue("$status", payload.Status);
		cmd.Parameters.AddWithValue("$uuid", payload.ClientUuid);
		cmd.Parameters.AddWithValue("$serverUpdated", row.ServerUpdatedAt.ToString("O"));
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

		using (var del = connection.CreateCommand())
		{
			del.Transaction = tx;
			del.CommandText = "DELETE FROM checklist_responses WHERE checklist_id = $id;";
			del.Parameters.AddWithValue("$id", targetId);
			await del.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		foreach (var response in payload.Responses)
		{
			long responseId;
			using (var ins = connection.CreateCommand())
			{
				ins.Transaction = tx;
				ins.CommandText = """
					INSERT INTO checklist_responses (
					    checklist_id, checklist_template_item_id, text_response, numeric_response,
					    boolean_response, selected_option_id)
					VALUES ($cid, $item, $text, $num, $bool, $opt);
					SELECT last_insert_rowid();
					""";
				ins.Parameters.AddWithValue("$cid", targetId);
				ins.Parameters.AddWithValue("$item", response.TemplateItemId);
				ins.Parameters.AddWithValue("$text", response.TextResponse ?? (object)DBNull.Value);
				ins.Parameters.AddWithValue("$num", response.NumericResponse ?? (object)DBNull.Value);
				ins.Parameters.AddWithValue("$bool", response.BooleanResponse.HasValue ? (response.BooleanResponse.Value ? 1 : 0) : DBNull.Value);
				ins.Parameters.AddWithValue("$opt", response.SelectedOptionId ?? (object)DBNull.Value);
				var scalar = await ins.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
				responseId = scalar is long l ? l : Convert.ToInt64(scalar ?? throw new InvalidOperationException("Не удалось сохранить ответ."));
			}

			if (response.MultiOptionIds is { Count: > 0 })
			{
				foreach (var optId in response.MultiOptionIds)
				{
					using var multi = connection.CreateCommand();
					multi.Transaction = tx;
					multi.CommandText = """
						INSERT OR IGNORE INTO checklist_response_multi_options (checklist_response_id, checklist_template_item_option_id)
						VALUES ($rid, $oid);
						""";
					multi.Parameters.AddWithValue("$rid", responseId);
					multi.Parameters.AddWithValue("$oid", optId);
					await multi.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				}
			}
		}
	}

	private static async Task<long> ResolveChecklistIdAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		string clientUuid,
		int serverId,
		CancellationToken cancellationToken)
	{
		using (var byUuid = connection.CreateCommand())
		{
			byUuid.Transaction = tx;
			byUuid.CommandText = "SELECT id FROM checklists WHERE client_uuid = $uuid LIMIT 1;";
			byUuid.Parameters.AddWithValue("$uuid", clientUuid);
			var match = await byUuid.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (match is long l)
				return l;
			if (match is int i)
				return i;
		}

		using var byId = connection.CreateCommand();
		byId.Transaction = tx;
		byId.CommandText = "SELECT client_uuid FROM checklists WHERE id = $id LIMIT 1;";
		byId.Parameters.AddWithValue("$id", serverId);
		var existingUuid = await byId.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		if (existingUuid is null or DBNull)
			return serverId;

		if (string.Equals(Convert.ToString(existingUuid), clientUuid, StringComparison.OrdinalIgnoreCase))
			return serverId;

		using var next = connection.CreateCommand();
		next.Transaction = tx;
		next.CommandText = "SELECT COALESCE(MAX(id), 0) + 1 FROM checklists;";
		var scalar = await next.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is long nl ? nl : Convert.ToInt64(scalar ?? serverId);
	}
}
