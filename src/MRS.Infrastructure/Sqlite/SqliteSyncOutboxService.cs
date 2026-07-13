using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteSyncOutboxService : ISyncOutboxService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteSyncOutboxService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public Task<int> CountPendingAsync(CancellationToken cancellationToken = default) =>
		CountPendingInternalAsync(cancellationToken);

	public async Task EnqueueAsync(SyncOutboxEnqueueRequest request, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);
		await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		long outboxId;
		using (var cmd = connection.CreateCommand())
		{
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT INTO sync_outbox (entity_type, local_client_uuid, operation, payload_json, created_at)
				VALUES ($type, $uuid, $op, $payload, datetime('now'));
				SELECT last_insert_rowid();
				""";
			cmd.Parameters.AddWithValue("$type", request.EntityType);
			cmd.Parameters.AddWithValue("$uuid", request.LocalClientUuid);
			cmd.Parameters.AddWithValue("$op", request.Operation);
			cmd.Parameters.AddWithValue("$payload", request.PayloadJson);
			var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			outboxId = scalar is long l ? l : Convert.ToInt64(scalar ?? throw new InvalidOperationException("Не удалось поставить в очередь."));
		}

		await SqliteSyncEntityLocks.InsertForOutboxAsync(connection, tx, outboxId, request.EntityType, request.PayloadJson, cancellationToken)
			.ConfigureAwait(false);
		await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<SyncOutboxEntry>> GetPendingAsync(int limit, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT id, entity_type, local_client_uuid, operation, payload_json, created_at, retry_count
			FROM sync_outbox
			WHERE processed_at IS NULL
			ORDER BY id
			LIMIT $limit;
			""";
		cmd.Parameters.AddWithValue("$limit", Math.Max(1, limit));

		var list = new List<SyncOutboxEntry>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var created = reader.GetString(5);
			DateTimeOffset createdAt = SqliteDateTimeParsing.TryParseStored(created, out var dt)
				? dt
				: DateTimeOffset.Now;
			list.Add(new SyncOutboxEntry(
				reader.GetInt64(0),
				reader.GetString(1),
				reader.GetString(2),
				reader.GetString(3),
				reader.IsDBNull(4) ? null : reader.GetString(4),
				createdAt,
				reader.GetInt32(6)));
		}

		return list;
	}

	public async Task MarkProcessedAsync(long outboxId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);
		await SqliteSyncEntityLocks.ReleaseForOutboxAsync(connection, outboxId, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			UPDATE sync_outbox
			SET processed_at = datetime('now'), error_message = NULL
			WHERE id = $id;
			""";
		cmd.Parameters.AddWithValue("$id", outboxId);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task MarkFailedAsync(long outboxId, string errorMessage, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			UPDATE sync_outbox
			SET retry_count = retry_count + 1, error_message = $err
			WHERE id = $id;
			""";
		cmd.Parameters.AddWithValue("$id", outboxId);
		cmd.Parameters.AddWithValue("$err", errorMessage);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task<int> CountPendingInternalAsync(CancellationToken cancellationToken)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT COUNT(*) FROM sync_outbox WHERE processed_at IS NULL;";
		var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is long l ? (int)l : Convert.ToInt32(scalar ?? 0);
	}
}
