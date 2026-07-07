using MRS.Application.Sync;

namespace MRS.Maui.Services;

public sealed class MauiServerConnectionSettings : IServerConnectionSettings
{
	private const string PrefUrl = "mrs.server_url";
	private const string PrefLastPull = "mrs.last_pull_at";

	public MauiServerConnectionSettings()
	{
#if ANDROID
		ServerBaseUrl = Preferences.Default.Get(PrefUrl, "http://10.0.2.2:5080");
#else
		ServerBaseUrl = Preferences.Default.Get(PrefUrl, "http://localhost:5080");
#endif
		var lastPull = Preferences.Default.Get(PrefLastPull, string.Empty);
		if (!string.IsNullOrWhiteSpace(lastPull) && DateTimeOffset.TryParse(lastPull, out var dt))
			LastPullAt = dt;
	}

	public string? ServerBaseUrl { get; set; }

	public DateTimeOffset? LastPullAt { get; set; }

	public event Action? Changed;

	public void Save()
	{
		Preferences.Default.Set(PrefUrl, ServerBaseUrl ?? string.Empty);
		Preferences.Default.Set(PrefLastPull, LastPullAt?.ToString("O") ?? string.Empty);
		Changed?.Invoke();
	}
}
