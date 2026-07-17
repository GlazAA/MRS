namespace MRS.Maui.Services;

/// <summary>Сохранение файла на устройство (обход ограничений BlazorWebView download).</summary>
public interface IAppFileSaveService
{
	/// <summary>Сохраняет файл. Возвращает путь или null, если пользователь отменил.</summary>
	Task<string?> SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken = default);
}
