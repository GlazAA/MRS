namespace MRS.Application.Sync;

public interface ISyncApplyService
{
	Task ApplyPullAsync(SyncPullResponse pull, CancellationToken cancellationToken = default);
}
