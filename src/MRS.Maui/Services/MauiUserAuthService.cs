using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MRS.Application.Security;
using MRS.Application.Sync;

namespace MRS.Maui.Services;

public sealed class MauiUserAuthService : IUserAuthService
{
	private const string PrefToken = "mrs.auth_token";
	private const string PrefLogin = "mrs.auth_login";

	private readonly IServerConnectionSettings _settings;
	private readonly ICurrentUserSession _session;
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

	public MauiUserAuthService(IServerConnectionSettings settings, ICurrentUserSession session)
	{
		_settings = settings;
		_session = session;
		AccessToken = ReadSecure(PrefToken);
		SavedLogin = Preferences.Default.Get(PrefLogin, string.Empty);
	}

	public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken);

	public string? AccessToken { get; private set; }

	public string? SavedLogin { get; private set; }

	public event Action? Changed;

	public async Task<AuthLoginResult> LoginAsync(string login, string password, CancellationToken cancellationToken = default)
	{
		var baseUrl = NormalizeBaseUrl(_settings.ServerBaseUrl);
		if (baseUrl is null)
			return new AuthLoginResult(false, "Укажите адрес сервера на главной странице.", null, null, null, null);

		try
		{
			using var client = CreateClient(baseUrl);
			using var response = await client.PostAsJsonAsync("/api/auth/login", new { login, password }, cancellationToken)
				.ConfigureAwait(false);
			var result = await response.Content.ReadFromJsonAsync<AuthLoginResult>(JsonOptions, cancellationToken)
				.ConfigureAwait(false);
			if (result is null)
				return new AuthLoginResult(false, "Пустой ответ сервера.", null, null, null, null);

			if (!result.Ok || string.IsNullOrWhiteSpace(result.AccessToken))
				return result with { AccessToken = null };

			AccessToken = result.AccessToken;
			SavedLogin = login.Trim();
			WriteSecure(PrefToken, AccessToken);
			Preferences.Default.Set(PrefLogin, SavedLogin);

			if (result.UserId is int uid && !string.IsNullOrWhiteSpace(result.RoleName))
			{
				var display = result.DisplayName ?? SavedLogin;
				await _session.SetAuthenticatedUserAsync(uid, result.RoleName, display, cancellationToken).ConfigureAwait(false);
			}

			Changed?.Invoke();
			return result;
		}
		catch (Exception ex)
		{
			return new AuthLoginResult(false, $"Не удалось войти: {ex.Message}", null, null, null, null);
		}
	}

	public async Task LogoutAsync(CancellationToken cancellationToken = default)
	{
		AccessToken = null;
		WriteSecure(PrefToken, null);
		await _session.ClearAuthenticatedUserAsync(cancellationToken).ConfigureAwait(false);
		Changed?.Invoke();
	}

	internal HttpClient CreateAuthorizedClient()
	{
		var baseUrl = NormalizeBaseUrl(_settings.ServerBaseUrl)
			?? throw new InvalidOperationException("Адрес сервера не задан.");
		var client = CreateClient(baseUrl);
		if (!string.IsNullOrWhiteSpace(AccessToken))
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
		return client;
	}

	private static HttpClient CreateClient(string baseUrl) =>
		new() { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(60) };

	private static string? NormalizeBaseUrl(string? url)
	{
		var trimmed = (url ?? string.Empty).Trim().TrimEnd('/');
		return trimmed.Length > 0 ? trimmed : null;
	}

	private static string? ReadSecure(string key)
	{
		try
		{
			return SecureStorage.Default.GetAsync(key).GetAwaiter().GetResult();
		}
		catch
		{
			return null;
		}
	}

	private static void WriteSecure(string key, string? value)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(value))
				SecureStorage.Default.Remove(key);
			else
				SecureStorage.Default.SetAsync(key, value).GetAwaiter().GetResult();
		}
		catch
		{
			// SecureStorage может быть недоступен на части платформ в отладке.
		}
	}
}
