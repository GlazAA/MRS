namespace MRS.Application.Users;

public sealed record EngineerDirectoryEntry(int UserId, string DisplayName);

public sealed record EngineerAdminEntry(
	int UserId,
	string LastName,
	string FirstName,
	string? MiddleName,
	string RoleName,
	bool IsActive)
{
	public string DisplayName
	{
		get
		{
			var middle = string.IsNullOrWhiteSpace(MiddleName) ? null : MiddleName.Trim();
			var label = middle is null
				? $"{LastName} {FirstName}".Trim()
				: $"{LastName} {FirstName} {middle}".Trim();
			return label.Length == 0 ? "Сотрудник" : label;
		}
	}
}

public sealed record CreateEngineerRequest(
	string LastName,
	string FirstName,
	string? MiddleName,
	string RoleName = "Инженер");

public interface IEngineerDirectoryService
{
	/// <summary>Активные инженеры (для выбора в КЛ / выездах).</summary>
	Task<IReadOnlyList<EngineerDirectoryEntry>> ListActiveAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<EngineerDirectoryEntry>> SearchAsync(string? query, CancellationToken cancellationToken = default);

	Task<EngineerDirectoryEntry> CreateAsync(CreateEngineerRequest request, CancellationToken cancellationToken = default);

	/// <summary>Все сотрудники для админки (инженер / менеджер / админ БД), включая скрытых.</summary>
	Task<IReadOnlyList<EngineerAdminEntry>> ListAllForAdminAsync(CancellationToken cancellationToken = default);

	Task UpdateAsync(int userId, CreateEngineerRequest request, CancellationToken cancellationToken = default);

	/// <summary>Мягкое удаление / восстановление (is_active).</summary>
	Task SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default);
}
