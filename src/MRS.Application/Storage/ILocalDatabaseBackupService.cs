namespace MRS.Application.Storage;

public sealed record LocalDatabaseBackupFile(string FileName, byte[] Content);

public interface ILocalDatabaseBackupService
{
	Task<LocalDatabaseBackupFile> CreateBackupAsync(CancellationToken cancellationToken = default);

	Task RestoreFromBackupAsync(byte[] backupContent, CancellationToken cancellationToken = default);
}
