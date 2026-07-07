namespace MRS.Application.Sync;

public interface IChecklistSyncPayloadService
{
	Task<string> BuildChecklistJsonAsync(int checklistId, string clientUuid, CancellationToken cancellationToken = default);

	Task EnsureClientUuidAsync(int checklistId, CancellationToken cancellationToken = default);

	Task<string?> GetClientUuidAsync(int checklistId, CancellationToken cancellationToken = default);

	Task MarkSyncedAsync(int checklistId, CancellationToken cancellationToken = default);
}
