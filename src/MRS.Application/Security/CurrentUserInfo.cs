namespace MRS.Application.Security;

public sealed record CurrentUserInfo(int UserId, string RoleName, string DisplayName);
