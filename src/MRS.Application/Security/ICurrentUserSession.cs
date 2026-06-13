namespace MRS.Application.Security;

/// <summary>Текущая роль пользователя в локальном клиенте (без сетевой авторизации).</summary>
public interface ICurrentUserSession
{
    CurrentUserInfo Current { get; }

    /// <summary>Роль «Администратор БД»: все полевые сценарии + SQL-окно и обработка обращений.</summary>
    bool IsDbAdministrator { get; }

    /// <summary>Роль «Инженер»: полевые сценарии без SQL-окна.</summary>
    bool IsEngineer { get; }

    /// <summary>Доступ к контрольным листам, объектам и управлению листами (обе роли).</summary>
    bool CanUseFieldFeatures { get; }

    Task SetRoleAsync(string roleName, CancellationToken cancellationToken = default);

    event Action? Changed;
}
