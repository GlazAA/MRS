using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteSyncOutboxQueryService : ISyncOutboxQueryService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteSyncOutboxQueryService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT COUNT(*) FROM sync_outbox WHERE processed_at IS NULL;";
		var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is long l ? (int)l : Convert.ToInt32(scalar ?? 0);
	}
}
