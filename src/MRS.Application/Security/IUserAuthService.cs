namespace MRS.Application.Security;

public sealed record AuthLoginResult(
	bool Ok,
	string? Error,
	int? UserId,
	string? RoleName,
	string? DisplayName,
	string? AccessToken);

public interface IUserAuthService
{
	bool IsAuthenticated { get; }

	string? AccessToken { get; }

	string? SavedLogin { get; }

	Task<AuthLoginResult> LoginAsync(string login, string password, CancellationToken cancellationToken = default);

	Task LogoutAsync(CancellationToken cancellationToken = default);

	event Action? Changed;
}
