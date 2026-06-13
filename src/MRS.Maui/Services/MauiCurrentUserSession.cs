using MRS.Application.Security;

namespace MRS.Maui.Services;

public sealed class MauiCurrentUserSession : ICurrentUserSession
{
    private const string PrefRole = "mrs.current_role";

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

    private static CurrentUserInfo EngineerUser() =>
        new(1, UserRoleNames.Engineer, "Демо Инженер");

    private static CurrentUserInfo DbAdminUser() =>
        new(2, UserRoleNames.DbAdministrator, "Демо Администратор БД");
}
