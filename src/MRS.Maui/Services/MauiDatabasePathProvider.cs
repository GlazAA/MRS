using MRS.Application.Storage;

namespace MRS.Maui.Services;

public sealed class MauiDatabasePathProvider : ILocalDatabasePath
{
	public string GetDatabaseFilePath() =>
		Path.Combine(FileSystem.AppDataDirectory, "mrs.db");
}
