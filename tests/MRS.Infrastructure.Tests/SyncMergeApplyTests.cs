using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Application.Sync;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

public class SyncMergeApplyTests
{
	[Fact]
	public async Task ApplyPull_skips_locked_organization_during_pending_push()
	{
		var path = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var paths = new FixedDbPath(path);
			var outbox = new SqliteSyncOutboxService(paths, bootstrapper);
			var apply = new SqliteSyncApplyService(paths, bootstrapper);

			await outbox.EnqueueAsync(new SyncOutboxEnqueueRequest(
				"organization",
				Guid.NewGuid().ToString(),
				"update",
				"""{"localId":1}"""), CancellationToken.None);

			await using (var connection = new SqliteConnection($"Data Source={path}"))
			{
				await connection.OpenAsync();
				using var lockCmd = connection.CreateCommand();
				lockCmd.CommandText = """
					INSERT INTO sync_entity_locks (outbox_id, entity_type, local_id)
					SELECT id, 'organization', 1 FROM sync_outbox LIMIT 1;
					""";
				await lockCmd.ExecuteNonQueryAsync();
			}

			await apply.ApplyPullAsync(new SyncPullResponse(
				DateTimeOffset.UtcNow,
				[new SyncOrganizationRow(1, "Серверное имя", null, true)],
				[], [], [], [], [], [], [], [], [], []));

			await using var verify = new SqliteConnection($"Data Source={path}");
			await verify.OpenAsync();
			using var read = verify.CreateCommand();
			read.CommandText = "SELECT full_name FROM organizations WHERE id = 1;";
			var name = (string?)await read.ExecuteScalarAsync();
			Assert.Equal("Мосархив", name);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task ApplyPull_inserts_engineer_note_from_server_by_client_uuid()
	{
		var path = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var apply = new SqliteSyncApplyService(new FixedDbPath(path), bootstrapper);
			var uuid = Guid.NewGuid().ToString();

			await apply.ApplyPullAsync(new SyncPullResponse(
				DateTimeOffset.UtcNow,
				[], [], [], [], [], [],
				[
					new SyncEngineerNotePullRow(
						uuid, 42, 1, "Текст с сервера", null, "Заголовок",
						1, null, null, false, null, DateTimeOffset.UtcNow)
				],
				[], [], [], []));

			await using var verify = new SqliteConnection($"Data Source={path}");
			await verify.OpenAsync();
			using var read = verify.CreateCommand();
			read.CommandText = "SELECT body, client_uuid, sync_state FROM engineer_notes WHERE id = 42;";
			await using var reader = await read.ExecuteReaderAsync();
			Assert.True(await reader.ReadAsync());
			Assert.Equal("Текст с сервера", reader.GetString(0));
			Assert.Equal(uuid, reader.GetString(1));
			Assert.Equal("synced", reader.GetString(2));
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task ApplyPull_allocates_new_id_when_server_id_collides_with_different_uuid()
	{
		var path = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var apply = new SqliteSyncApplyService(new FixedDbPath(path), bootstrapper);

			await using (var connection = new SqliteConnection($"Data Source={path}"))
			{
				await connection.OpenAsync();
				using var seed = connection.CreateCommand();
				seed.CommandText = """
					INSERT INTO engineer_notes (
					    id, author_user_id, body, is_completed, client_uuid, sync_state, created_at, updated_at)
					VALUES (42, 1, 'Локальная', 0, 'local-uuid', 'synced', datetime('now'), datetime('now'));
					""";
				await seed.ExecuteNonQueryAsync();
			}

			await apply.ApplyPullAsync(new SyncPullResponse(
				DateTimeOffset.UtcNow,
				[], [], [], [], [], [],
				[
					new SyncEngineerNotePullRow(
						Guid.NewGuid().ToString(), 42, 1, "С сервера", null, null,
						null, null, null, false, null, DateTimeOffset.UtcNow)
				],
				[], [], [], []));

			await using var verify = new SqliteConnection($"Data Source={path}");
			await verify.OpenAsync();
			using var count = verify.CreateCommand();
			count.CommandText = "SELECT COUNT(*) FROM engineer_notes;";
			Assert.Equal(2, Convert.ToInt32(await count.ExecuteScalarAsync()));
		}
		finally
		{
			Cleanup(path);
		}
	}

	private static string CreateTempDbPath() =>
		Path.Combine(Path.GetTempPath(), $"mrs_sync_merge_{Guid.NewGuid():N}.db");

	private static void Cleanup(string path)
	{
		SqliteConnection.ClearAllPools();
		if (File.Exists(path))
			File.Delete(path);
	}

	private sealed class FixedDbPath(string path) : ILocalDatabasePath
	{
		public string GetDatabaseFilePath() => path;
	}
}
