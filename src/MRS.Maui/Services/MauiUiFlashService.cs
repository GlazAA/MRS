namespace MRS.Maui.Services;

public sealed class MauiUiFlashService : IUiFlashService
{
	private string? _message;

	public void Set(string message)
	{
		_message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
	}

	public string? Consume()
	{
		var msg = _message;
		_message = null;
		return msg;
	}
}
