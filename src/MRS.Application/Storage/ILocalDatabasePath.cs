namespace MRS.Application.Storage;

/// <summary>Путь к файлу локальной SQLite (AppData на устройстве).</summary>
public interface ILocalDatabasePath
{
	string GetDatabaseFilePath();
}
