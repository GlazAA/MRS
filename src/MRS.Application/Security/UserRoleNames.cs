namespace MRS.Application.Security;

public static class UserRoleNames
{
	public const string Engineer = "Инженер";
	public const string Manager = "Менеджер";
	public const string DbAdministrator = "Администратор БД";

	/// <summary>Роли, которые можно назначить коллеге в справочнике.</summary>
	public static IReadOnlyList<string> AssignableRoles { get; } =
	[
		Engineer,
		Manager,
		DbAdministrator
	];

	public static bool IsAssignable(string? roleName) =>
		roleName is not null &&
		AssignableRoles.Any(r => string.Equals(r, roleName, StringComparison.Ordinal));

	public static string NormalizeOrDefault(string? roleName) =>
		IsAssignable(roleName) ? AssignableRoles.First(r => string.Equals(r, roleName, StringComparison.Ordinal)) : Engineer;
}
