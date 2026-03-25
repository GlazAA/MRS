using Microsoft.Data.Sqlite;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

internal static class SqliteLocalDatabase
{
	internal static async Task<SqliteConnection> OpenReadyAsync(
		ILocalDatabasePath paths,
		ILocalDatabaseBootstrapper bootstrapper,
		CancellationToken cancellationToken)
	{
		var status = await bootstrapper.EnsureReadyAsync(paths.GetDatabaseFilePath(), cancellationToken).ConfigureAwait(false);
		if (!status.Ready)
			throw new InvalidOperationException(status.Error ?? "База данных недоступна.");

		var b = new SqliteConnectionStringBuilder
		{
			DataSource = paths.GetDatabaseFilePath(),
			Mode = SqliteOpenMode.ReadWriteCreate,
			Cache = SqliteCacheMode.Shared,
			ForeignKeys = true
		};
		var connection = new SqliteConnection(b.ToString());
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		return connection;
	}
}
