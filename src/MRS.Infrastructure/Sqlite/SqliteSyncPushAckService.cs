using System.Text.Json;
using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteSyncPushAckService : ISyncPushAckService
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;
	private readonly IChecklistSyncPayloadService _checklistSync;

	public SqliteSyncPushAckService(
		ILocalDatabasePath paths,
		ILocalDatabaseBootstrapper bootstrapper,
		IChecklistSyncPayloadService checklistSync)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
		_checklistSync = checklistSync;
	}

	public async Task MarkAcknowledgedAsync(SyncOutboxEntry entry, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(entry.PayloadJson))
			return;

		if (string.Equals(entry.EntityType, "checklist", StringComparison.OrdinalIgnoreCase))
		{
			var payload = JsonSerializer.Deserialize<ChecklistSyncPayload>(entry.PayloadJson, JsonOptions);
			if (payload is not null)
				await _checklistSync.MarkSyncedAsync(payload.LocalId, cancellationToken).ConfigureAwait(false);
			return;
		}

		if (string.Equals(entry.EntityType, "engineer_note", StringComparison.OrdinalIgnoreCase))
		{
			var payload = JsonSerializer.Deserialize<EngineerNoteSyncPayload>(entry.PayloadJson, JsonOptions);
			if (payload is not null && payload.LocalId > 0 && !string.Equals(entry.Operation, "delete", StringComparison.OrdinalIgnoreCase))
				await MarkTableSyncedAsync("engineer_notes", payload.LocalId, cancellationToken).ConfigureAwait(false);
			return;
		}

		if (string.Equals(entry.EntityType, "scheduled_visit", StringComparison.OrdinalIgnoreCase))
		{
			var payload = JsonSerializer.Deserialize<ScheduledVisitSyncPayload>(entry.PayloadJson, JsonOptions);
			if (payload is not null && payload.LocalId > 0)
				await MarkTableSyncedAsync("scheduled_visits", payload.LocalId, cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task MarkTableSyncedAsync(string table, int localId, CancellationToken cancellationToken)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = $"""
			UPDATE {table}
			SET sync_state = 'synced', updated_at = datetime('now')
			WHERE id = $id;
			""";
		cmd.Parameters.AddWithValue("$id", localId);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}
}
