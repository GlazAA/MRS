using Microsoft.Data.Sqlite;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

internal static class SqliteSyncEntityLocks
{
	internal static async Task InsertForOutboxAsync(
		SqliteConnection connection,
		SqliteTransaction? tx,
		long outboxId,
		string entityType,
		string payloadJson,
		CancellationToken cancellationToken)
	{
		foreach (var entity in SyncEntityLockExtractor.Extract(entityType, payloadJson))
		{
			using var cmd = connection.CreateCommand();
			if (tx is not null)
				cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT OR IGNORE INTO sync_entity_locks (outbox_id, entity_type, local_id)
				VALUES ($oid, $type, $id);
				""";
			cmd.Parameters.AddWithValue("$oid", outboxId);
			cmd.Parameters.AddWithValue("$type", entity.EntityType);
			cmd.Parameters.AddWithValue("$id", entity.LocalId);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	internal static async Task ReleaseForOutboxAsync(
		SqliteConnection connection,
		long outboxId,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "DELETE FROM sync_entity_locks WHERE outbox_id = $id;";
		cmd.Parameters.AddWithValue("$id", outboxId);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}
}
