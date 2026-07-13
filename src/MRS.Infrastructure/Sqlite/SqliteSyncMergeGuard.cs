using Microsoft.Data.Sqlite;

namespace MRS.Infrastructure.Sqlite;

internal static class SqliteSyncMergeGuard
{
	internal static async Task<bool> IsLockedAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		string entityType,
		long localId,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.Transaction = tx;
		cmd.CommandText = """
			SELECT 1
			FROM sync_entity_locks
			WHERE entity_type = $type AND local_id = $id
			LIMIT 1;
			""";
		cmd.Parameters.AddWithValue("$type", entityType);
		cmd.Parameters.AddWithValue("$id", localId);
		return await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
	}

	internal static async Task<bool> ShouldSkipReferenceUpsertAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		string entityType,
		long serverId,
		CancellationToken cancellationToken) =>
		await IsLockedAsync(connection, tx, entityType, serverId, cancellationToken).ConfigureAwait(false);

	internal static async Task<bool> ShouldSkipUuidEntityAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		string tableName,
		string entityType,
		string clientUuid,
		long serverId,
		DateTimeOffset serverUpdatedAt,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.Transaction = tx;
		cmd.CommandText = $"""
			SELECT id, sync_state, updated_at
			FROM {tableName}
			WHERE client_uuid = $uuid OR id = $id
			LIMIT 1;
			""";
		cmd.Parameters.AddWithValue("$uuid", clientUuid);
		cmd.Parameters.AddWithValue("$id", serverId);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			return false;

		var localId = reader.GetInt32(0);
		var syncState = reader.GetString(1);
		if (await IsLockedAsync(connection, tx, entityType, localId, cancellationToken).ConfigureAwait(false))
			return true;

		if (string.Equals(syncState, "pending_upload", StringComparison.OrdinalIgnoreCase))
			return true;

		if (!reader.IsDBNull(2))
		{
			var raw = reader.GetString(2);
			if (SqliteDateTimeParsing.TryParseStored(raw, out var localUpdated) && localUpdated > serverUpdatedAt)
				return true;
		}

		return false;
	}

	internal static async Task<long> ResolveInsertIdAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		string tableName,
		long serverId,
		string identityColumn,
		string identityValue,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.Transaction = tx;
		cmd.CommandText = $"""
			SELECT id, {identityColumn}
			FROM {tableName}
			WHERE id = $id
			LIMIT 1;
			""";
		cmd.Parameters.AddWithValue("$id", serverId);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			return serverId;

		var existingIdentity = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
		if (string.Equals(existingIdentity, identityValue, StringComparison.Ordinal))
			return serverId;

		using var nextCmd = connection.CreateCommand();
		nextCmd.Transaction = tx;
		nextCmd.CommandText = $"SELECT COALESCE(MAX(id), 0) + 1 FROM {tableName};";
		var scalar = await nextCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is long l ? l : Convert.ToInt64(scalar ?? serverId);
	}
}
