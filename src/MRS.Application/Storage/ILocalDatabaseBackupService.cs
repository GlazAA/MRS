namespace MRS.Application.Storage;

public sealed record LocalDatabaseBackupFile(string FileName, byte[] Content);

public enum LocalBackupFormat
{
	/// <summary>Сырой SQLite (.db) — удобно для ПК.</summary>
	SqliteDb = 0,

	/// <summary>JSON-обёртка с base64 БД — удобно для телефона (Share).</summary>
	JsonEnvelope = 1
}

public interface ILocalDatabaseBackupService
{
	Task<LocalDatabaseBackupFile> CreateBackupAsync(
		LocalBackupFormat format = LocalBackupFormat.SqliteDb,
		CancellationToken cancellationToken = default);

	Task RestoreFromBackupAsync(byte[] backupContent, CancellationToken cancellationToken = default);
}
