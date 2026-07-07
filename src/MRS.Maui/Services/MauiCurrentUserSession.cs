using MRS.Application.Security;

namespace MRS.Maui.Services;

public sealed class MauiCurrentUserSession : ICurrentUserSession
{
    private const string PrefRole = "mrs.current_role";
    private const string PrefEngineerName = "mrs.engineer_display_name";
    private const string PrefAuthUserId = "mrs.auth_user_id";
    private const string PrefAuthRole = "mrs.auth_role_name";
    private const string PrefAuthDisplay = "mrs.auth_display_name";

    private CurrentUserInfo _current = EngineerUser();

    public MauiCurrentUserSession()
    {
        if (TryRestoreAuthenticatedUser(out var authUser))
        {
            _current = authUser;
            return;
        }

        var saved = Preferences.Default.Get(PrefRole, UserRoleNames.Engineer);
        _current = saved == UserRoleNames.DbAdministrator ? DbAdminUser() : EngineerUser();
    }

    public CurrentUserInfo Current => _current;

    public bool IsDbAdministrator => _current.RoleName == UserRoleNames.DbAdministrator;

    public bool IsEngineer => _current.RoleName == UserRoleNames.Engineer;

    public bool CanUseFieldFeatures => IsEngineer || IsDbAdministrator;

    public event Action? Changed;

    public Task SetRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        ClearAuthPrefs();
        _current = roleName == UserRoleNames.DbAdministrator ? DbAdminUser() : EngineerUser();
        Preferences.Default.Set(PrefRole, _current.RoleName);
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task SetEngineerDisplayNameAsync(string displayName, CancellationToken cancellationToken = default)
    {
        var trimmed = (displayName ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("Укажите ФИО инженера.");

        Preferences.Default.Set(PrefEngineerName, trimmed);
        if (IsEngineer)
            _current = _current with { DisplayName = trimmed };

        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task SetAuthenticatedUserAsync(int userId, string roleName, string displayName, CancellationToken cancellationToken = default)
    {
        _current = new CurrentUserInfo(userId, roleName, displayName);
        Preferences.Default.Set(PrefAuthUserId, userId);
        Preferences.Default.Set(PrefAuthRole, roleName);
        Preferences.Default.Set(PrefAuthDisplay, displayName);
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task ClearAuthenticatedUserAsync(CancellationToken cancellationToken = default)
    {
        ClearAuthPrefs();
        var saved = Preferences.Default.Get(PrefRole, UserRoleNames.Engineer);
        _current = saved == UserRoleNames.DbAdministrator ? DbAdminUser() : EngineerUser();
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    private static bool TryRestoreAuthenticatedUser(out CurrentUserInfo user)
    {
        user = default!;
        var userId = Preferences.Default.Get(PrefAuthUserId, 0);
        var role = Preferences.Default.Get(PrefAuthRole, string.Empty);
        var display = Preferences.Default.Get(PrefAuthDisplay, string.Empty);
        if (userId <= 0 || string.IsNullOrWhiteSpace(role))
            return false;

        user = new CurrentUserInfo(userId, role, string.IsNullOrWhiteSpace(display) ? role : display);
        return true;
    }

    private static void ClearAuthPrefs()
    {
        Preferences.Default.Remove(PrefAuthUserId);
        Preferences.Default.Remove(PrefAuthRole);
        Preferences.Default.Remove(PrefAuthDisplay);
    }

    private static CurrentUserInfo EngineerUser()
    {
        var name = Preferences.Default.Get(PrefEngineerName, "Демо Инженер");
        return new(1, UserRoleNames.Engineer, name);
    }

    private static CurrentUserInfo DbAdminUser() =>
        new(2, UserRoleNames.DbAdministrator, "Демо Администратор БД");
}
