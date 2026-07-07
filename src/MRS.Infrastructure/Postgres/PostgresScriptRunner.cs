using Npgsql;

namespace MRS.Infrastructure.Postgres;

internal static class PostgresScriptRunner
{
	internal static IReadOnlyList<string> SplitStatements(string sql) =>
		Sqlite.SqliteScriptRunner.SplitStatements(sql);

	internal static async Task ExecuteScriptAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
	{
		foreach (var statement in SplitStatements(sql))
		{
			cancellationToken.ThrowIfCancellationRequested();
			await using var cmd = new NpgsqlCommand(statement, connection);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
	}
}
