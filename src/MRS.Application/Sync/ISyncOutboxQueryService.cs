namespace MRS.Application.Sync;

public interface ISyncOutboxQueryService
{
	Task<int> CountPendingAsync(CancellationToken cancellationToken = default);
}
