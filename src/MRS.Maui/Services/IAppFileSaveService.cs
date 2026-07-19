namespace MRS.Maui.Services;

/// <summary>Сохранение файла на устройство (обход ограничений BlazorWebView download).</summary>
public interface IAppFileSaveService
{
	/// <summary>
	/// Сохраняет файл.
	/// Windows: папка «Загрузки» + проводник.
	/// Android/iOS: системное меню «Поделиться / Сохранить».
	/// Возвращает путь или null, если пользователь отменил.
	/// </summary>
	Task<string?> SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken = default);
}
