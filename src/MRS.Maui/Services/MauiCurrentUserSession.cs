using MRS.Application.Security;

namespace MRS.Maui.Services;

public sealed class MauiCurrentUserSession : ICurrentUserSession
{
    private const string PrefRole = "mrs.current_role";
    private const string PrefEngineerName = "mrs.engineer_display_name";

    private CurrentUserInfo _current = EngineerUser();

    public MauiCurrentUserSession()
    {
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

    private static CurrentUserInfo EngineerUser()
    {
        var name = Preferences.Default.Get(PrefEngineerName, "Демо Инженер");
        return new(1, UserRoleNames.Engineer, name);
    }

    private static CurrentUserInfo DbAdminUser() =>
        new(2, UserRoleNames.DbAdministrator, "Демо Администратор БД");
}
