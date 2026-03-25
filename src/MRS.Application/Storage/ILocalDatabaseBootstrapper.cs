namespace MRS.Application.Storage;

/// <summary>Создаёт файл БД при необходимости и применяет миграции до текущей версии.</summary>
public interface ILocalDatabaseBootstrapper
{
	Task<LocalDatabaseStatus> EnsureReadyAsync(string databaseFilePath, CancellationToken cancellationToken = default);
}
