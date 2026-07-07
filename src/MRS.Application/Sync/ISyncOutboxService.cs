namespace MRS.Application.Sync;

public interface ISyncOutboxService : ISyncOutboxQueryService
{
	Task EnqueueAsync(SyncOutboxEnqueueRequest request, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<SyncOutboxEntry>> GetPendingAsync(int limit, CancellationToken cancellationToken = default);

	Task MarkProcessedAsync(long outboxId, CancellationToken cancellationToken = default);

	Task MarkFailedAsync(long outboxId, string errorMessage, CancellationToken cancellationToken = default);
}
