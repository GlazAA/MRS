using Microsoft.Data.Sqlite;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteLocalDatabaseBackupService : ILocalDatabaseBackupService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteLocalDatabaseBackupService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<LocalDatabaseBackupFile> CreateBackupAsync(CancellationToken cancellationToken = default)
	{
		var tempPath = Path.Combine(Path.GetTempPath(), $"mrs-backup-{Guid.NewGuid():N}.db");
		try
		{
			await using (var source = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
				.ConfigureAwait(false))
			{
				var destBuilder = new SqliteConnectionStringBuilder { DataSource = tempPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false };
				await using var destination = new SqliteConnection(destBuilder.ToString());
				await destination.OpenAsync(cancellationToken).ConfigureAwait(false);
				source.BackupDatabase(destination);
			}

			var bytes = await File.ReadAllBytesAsync(tempPath, cancellationToken).ConfigureAwait(false);
			var fileName = $"mrs-backup-{DateTime.Now:yyyyMMdd-HHmm}.db";
			return new LocalDatabaseBackupFile(fileName, bytes);
		}
		finally
		{
			SqliteConnection.ClearAllPools();
			if (File.Exists(tempPath))
				File.Delete(tempPath);
		}
	}

	public Task RestoreFromBackupAsync(byte[] backupContent, CancellationToken cancellationToken = default)
	{
		if (backupContent is null || backupContent.Length < 16)
			throw new InvalidOperationException("Файл резервной копии пуст или повреждён.");

		if (!LooksLikeSqlite(backupContent))
			throw new InvalidOperationException("Выбранный файл не похож на базу SQLite (.db).");

		var dbPath = _paths.GetDatabaseFilePath();
		var dir = Path.GetDirectoryName(dbPath);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);

		SqliteConnection.ClearAllPools();

		var tempPath = dbPath + $".restore-{Guid.NewGuid():N}";
		File.WriteAllBytes(tempPath, backupContent);

		if (File.Exists(dbPath))
			File.Delete(dbPath);

		File.Move(tempPath, dbPath);
		return Task.CompletedTask;
	}

	private static bool LooksLikeSqlite(byte[] content) =>
		content.Length >= 16
		&& content[0] == (byte)'S'
		&& content[1] == (byte)'Q'
		&& content[2] == (byte)'L'
		&& content[3] == (byte)'i';
}
