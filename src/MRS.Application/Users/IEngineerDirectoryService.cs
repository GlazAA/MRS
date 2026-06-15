namespace MRS.Application.Users;

public sealed record EngineerDirectoryEntry(int UserId, string DisplayName);

public interface IEngineerDirectoryService
{
	Task<IReadOnlyList<EngineerDirectoryEntry>> ListActiveAsync(CancellationToken cancellationToken = default);
}
