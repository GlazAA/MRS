using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

public class LocalDatabaseBackupJsonTests
{
	[Fact]
	public async Task JsonEnvelope_backup_roundtrips_through_restore()
	{
		var sourcePath = CreateTempDbPath("src");
		var restorePath = CreateTempDbPath("dst");
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(sourcePath)).Ready);
			await TestDemoOperationalSeed.EnsureAsync(sourcePath, bootstrapper);

			var sourcePaths = new FixedDbPath(sourcePath);
			var backup = new SqliteLocalDatabaseBackupService(sourcePaths, bootstrapper);
			var file = await backup.CreateBackupAsync(LocalBackupFormat.JsonEnvelope);

			Assert.EndsWith(".json", file.FileName, StringComparison.OrdinalIgnoreCase);
			Assert.Equal((byte)'{', file.Content[0]);

			using (var doc = JsonDocument.Parse(file.Content))
			{
				Assert.Equal("mrs-sqlite-json-v1", doc.RootElement.GetProperty("format").GetString());
				Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("sqliteBase64").GetString()));
				Assert.EndsWith(".db", doc.RootElement.GetProperty("originalDbFileName").GetString(), StringComparison.OrdinalIgnoreCase);
			}

			// Целевая БД должна существовать как файл пути (restore заменяет содержимое).
			Assert.True((await bootstrapper.EnsureReadyAsync(restorePath)).Ready);
			var restorePaths = new FixedDbPath(restorePath);
			var restore = new SqliteLocalDatabaseBackupService(restorePaths, bootstrapper);
			await restore.RestoreFromBackupAsync(file.Content);

			await using var conn = new SqliteConnection($"Data Source={restorePath}");
			await conn.OpenAsync();
			using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT COUNT(*) FROM organizations WHERE is_active = 1;";
			var orgs = Convert.ToInt32(await cmd.ExecuteScalarAsync());
			Assert.True(orgs >= 3, $"Expected restored demo orgs, got {orgs}");

			cmd.CommandText = "SELECT COUNT(*) FROM maintenance_types;";
			Assert.Equal(9, Convert.ToInt32(await cmd.ExecuteScalarAsync()));
		}
		finally
		{
			Cleanup(sourcePath);
			Cleanup(restorePath);
		}
	}

	[Fact]
	public async Task Mobile_menu_format_choice_is_json_envelope()
	{
		// Контракт UI: на телефоне CreateBackupAsync(JsonEnvelope) → .json с base64 SQLite.
		var path = CreateTempDbPath("mobile");
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var backup = new SqliteLocalDatabaseBackupService(new FixedDbPath(path), bootstrapper);
			var file = await backup.CreateBackupAsync(LocalBackupFormat.JsonEnvelope);
			Assert.EndsWith(".json", file.FileName, StringComparison.OrdinalIgnoreCase);
			var text = Encoding.UTF8.GetString(file.Content);
			Assert.Contains("\"format\":\"mrs-sqlite-json-v1\"", text, StringComparison.Ordinal);
			Assert.Contains("\"sqliteBase64\":", text, StringComparison.Ordinal);
		}
		finally
		{
			Cleanup(path);
		}
	}

	private static string CreateTempDbPath(string tag) =>
		Path.Combine(Path.GetTempPath(), $"mrs_bak_{tag}_{Guid.NewGuid():N}.db");

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
