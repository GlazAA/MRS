namespace MRS.Application.Sync;

public sealed record ServerSyncResult(
	bool NetworkAvailable,
	bool Ok,
	string Message,
	int PendingOutboxCount,
	DateTimeOffset CompletedAt);

public interface IServerSyncService
{
	ServerSyncResult? LastResult { get; }

	event Action? Changed;

	Task<ServerSyncResult> SyncAsync(CancellationToken cancellationToken = default);
}
