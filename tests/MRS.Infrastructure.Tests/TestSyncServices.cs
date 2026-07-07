using MRS.Application.Storage;
using MRS.Application.Sync;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

internal sealed class NoOpSyncOutboxService : ISyncOutboxService
{
	public Task<int> CountPendingAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult(0);

	public Task EnqueueAsync(SyncOutboxEnqueueRequest request, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;

	public Task<IReadOnlyList<SyncOutboxEntry>> GetPendingAsync(int limit, CancellationToken cancellationToken = default) =>
		Task.FromResult<IReadOnlyList<SyncOutboxEntry>>(Array.Empty<SyncOutboxEntry>());

	public Task MarkProcessedAsync(long outboxId, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;

	public Task MarkFailedAsync(long outboxId, string errorMessage, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;
}

internal static class TestSyncServices
{
	public static (ISyncOutboxService Outbox, IChecklistSyncPayloadService Payload) Create(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper) =>
		(new NoOpSyncOutboxService(), new SqliteChecklistSyncPayloadService(paths, bootstrapper));

	public static SqliteChecklistEditService CreateEditService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		var (outbox, payload) = Create(paths, bootstrapper);
		return new SqliteChecklistEditService(paths, bootstrapper, outbox, payload);
	}
}
