using System.Diagnostics;

namespace MRS.Maui.Services;

/// <summary>
/// Сохранение файла в папку «Загрузки» пользователя и показ в проводнике.
/// Обходит ограничения BlazorWebView (download) и нестабильный FileSavePicker в unpackaged MAUI.
/// </summary>
public sealed class MauiAppFileSaveService : IAppFileSaveService
{
	public async Task<string?> SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(content);
		if (string.IsNullOrWhiteSpace(fileName))
			fileName = "download.bin";

		fileName = Path.GetFileName(fileName.Trim());

		var folder = GetDownloadsFolder();
		Directory.CreateDirectory(folder);

		var path = MakeUniquePath(folder, fileName);
		await File.WriteAllBytesAsync(path, content, cancellationToken).ConfigureAwait(true);

		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = $"/select,\"{path}\"",
				UseShellExecute = true
			});
		}
		catch
		{
			// Файл уже сохранён — открытие проводника не критично.
		}

		return path;
	}

	private static string GetDownloadsFolder()
	{
		var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		var downloads = Path.Combine(profile, "Downloads");
		if (Directory.Exists(downloads))
			return downloads;

		return FileSystem.AppDataDirectory;
	}

	private static string MakeUniquePath(string folder, string fileName)
	{
		var path = Path.Combine(folder, fileName);
		if (!File.Exists(path))
			return path;

		var stem = Path.GetFileNameWithoutExtension(fileName);
		var ext = Path.GetExtension(fileName);
		for (var i = 2; i < 1000; i++)
		{
			var candidate = Path.Combine(folder, $"{stem}_{i}{ext}");
			if (!File.Exists(candidate))
				return candidate;
		}

		return Path.Combine(folder, $"{stem}_{DateTime.Now:HHmmss}{ext}");
	}
}
