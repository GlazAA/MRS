using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteLocalDatabaseBackupService : ILocalDatabaseBackupService
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteLocalDatabaseBackupService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<LocalDatabaseBackupFile> CreateBackupAsync(
		LocalBackupFormat format = LocalBackupFormat.SqliteDb,
		CancellationToken cancellationToken = default)
	{
		var dbBytes = await CreateSqliteBytesAsync(cancellationToken).ConfigureAwait(false);
		var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");

		if (format == LocalBackupFormat.JsonEnvelope)
		{
			var envelope = new MrsJsonBackupEnvelope(
				"mrs-sqlite-json-v1",
				DateTimeOffset.Now.ToString("O"),
				$"mrs-backup-{stamp}.db",
				Convert.ToBase64String(dbBytes));
			var json = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
			return new LocalDatabaseBackupFile($"mrs-backup-{stamp}.json", json);
		}

		return new LocalDatabaseBackupFile($"mrs-backup-{stamp}.db", dbBytes);
	}

	public Task RestoreFromBackupAsync(byte[] backupContent, CancellationToken cancellationToken = default)
	{
		if (backupContent is null || backupContent.Length < 16)
			throw new InvalidOperationException("Файл резервной копии пуст или повреждён.");

		var sqliteBytes = UnwrapIfJson(backupContent);

		if (!LooksLikeSqlite(sqliteBytes))
			throw new InvalidOperationException("Выбранный файл не похож на базу SQLite (.db / .json).");

		var dbPath = _paths.GetDatabaseFilePath();
		var dir = Path.GetDirectoryName(dbPath);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);

		SqliteConnection.ClearAllPools();

		var tempPath = dbPath + $".restore-{Guid.NewGuid():N}";
		File.WriteAllBytes(tempPath, sqliteBytes);

		if (File.Exists(dbPath))
			File.Delete(dbPath);

		File.Move(tempPath, dbPath);
		return Task.CompletedTask;
	}

	private async Task<byte[]> CreateSqliteBytesAsync(CancellationToken cancellationToken)
	{
		var tempPath = Path.Combine(Path.GetTempPath(), $"mrs-backup-{Guid.NewGuid():N}.db");
		try
		{
			await using (var source = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
				.ConfigureAwait(false))
			{
				var destBuilder = new SqliteConnectionStringBuilder
				{
					DataSource = tempPath,
					Mode = SqliteOpenMode.ReadWriteCreate,
					Pooling = false
				};
				await using var destination = new SqliteConnection(destBuilder.ToString());
				await destination.OpenAsync(cancellationToken).ConfigureAwait(false);
				source.BackupDatabase(destination);
			}

			return await File.ReadAllBytesAsync(tempPath, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			SqliteConnection.ClearAllPools();
			if (File.Exists(tempPath))
				File.Delete(tempPath);
		}
	}

	private static byte[] UnwrapIfJson(byte[] content)
	{
		if (!LooksLikeJson(content))
			return content;

		try
		{
			var envelope = JsonSerializer.Deserialize<MrsJsonBackupEnvelope>(content, JsonOptions);
			if (envelope is null || string.IsNullOrWhiteSpace(envelope.SqliteBase64))
				throw new InvalidOperationException("JSON-копия не содержит данных БД.");
			return Convert.FromBase64String(envelope.SqliteBase64);
		}
		catch (Exception ex) when (ex is not InvalidOperationException)
		{
			throw new InvalidOperationException("Не удалось прочитать JSON-копию БД.", ex);
		}
	}

	private static bool LooksLikeJson(byte[] content)
	{
		for (var i = 0; i < content.Length; i++)
		{
			var b = content[i];
			if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
				continue;
			return b == (byte)'{';
		}

		return false;
	}

	private static bool LooksLikeSqlite(byte[] content) =>
		content.Length >= 16
		&& content[0] == (byte)'S'
		&& content[1] == (byte)'Q'
		&& content[2] == (byte)'L'
		&& content[3] == (byte)'i';

	private sealed record MrsJsonBackupEnvelope(
		[property: System.Text.Json.Serialization.JsonPropertyName("format")] string Format,
		[property: System.Text.Json.Serialization.JsonPropertyName("createdAt")] string CreatedAt,
		[property: System.Text.Json.Serialization.JsonPropertyName("originalDbFileName")] string OriginalDbFileName,
		[property: System.Text.Json.Serialization.JsonPropertyName("sqliteBase64")] string SqliteBase64);
}
