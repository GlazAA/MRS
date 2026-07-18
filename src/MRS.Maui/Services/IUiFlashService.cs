namespace MRS.Maui.Services;

/// <summary>Одноразовое сообщение успеха после перехода на другую страницу.</summary>
public interface IUiFlashService
{
	void Set(string message);

	/// <summary>Забирает сообщение (один раз) или null.</summary>
	string? Consume();
}
