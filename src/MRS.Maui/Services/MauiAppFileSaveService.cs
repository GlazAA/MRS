using System.Diagnostics;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace MRS.Maui.Services;

/// <summary>
/// Сохранение файла: на Windows — в «Загрузки», на телефоне — через системное «Поделиться».
/// </summary>
public sealed class MauiAppFileSaveService : IAppFileSaveService
{
	public async Task<string?> SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(content);
		if (string.IsNullOrWhiteSpace(fileName))
			fileName = "download.bin";

		fileName = Path.GetFileName(fileName.Trim());

#if WINDOWS
		return await SaveWindowsAsync(fileName, content, cancellationToken).ConfigureAwait(true);
#else
		return await ShareMobileAsync(fileName, content, cancellationToken).ConfigureAwait(true);
#endif
	}

#if WINDOWS
	private static async Task<string?> SaveWindowsAsync(string fileName, byte[] content, CancellationToken cancellationToken)
	{
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
			// Файл уже сохранён.
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
#else
	private static async Task<string?> ShareMobileAsync(string fileName, byte[] content, CancellationToken cancellationToken)
	{
		var folder = FileSystem.CacheDirectory;
		Directory.CreateDirectory(folder);
		var path = MakeUniquePath(folder, fileName);
		await File.WriteAllBytesAsync(path, content, cancellationToken).ConfigureAwait(true);

		await Share.Default.RequestAsync(new ShareFileRequest
		{
			Title = fileName,
			File = new ShareFile(path, GuessContentType(fileName))
		}).ConfigureAwait(true);

		return path;
	}

	private static string GuessContentType(string fileName)
	{
		if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
			return "application/json";
		if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
			return "application/pdf";
		if (fileName.EndsWith(".doc", StringComparison.OrdinalIgnoreCase)
		    || fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
			return "application/msword";
		if (fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
		    || fileName.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase))
			return "application/octet-stream";
		return "application/octet-stream";
	}
#endif

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
