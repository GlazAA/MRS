namespace MRS.Application.Users;

public sealed record EngineerDirectoryEntry(int UserId, string DisplayName);

public sealed record CreateEngineerRequest(
	string LastName,
	string FirstName,
	string? MiddleName);

public interface IEngineerDirectoryService
{
	Task<IReadOnlyList<EngineerDirectoryEntry>> ListActiveAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<EngineerDirectoryEntry>> SearchAsync(string? query, CancellationToken cancellationToken = default);

	Task<EngineerDirectoryEntry> CreateAsync(CreateEngineerRequest request, CancellationToken cancellationToken = default);
}
