namespace MRS.Application.Sync;

public interface ISyncPushAckService
{
	Task MarkAcknowledgedAsync(SyncOutboxEntry entry, CancellationToken cancellationToken = default);
}
