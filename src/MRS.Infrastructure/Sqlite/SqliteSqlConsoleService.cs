using Microsoft.Data.Sqlite;
using MRS.Application.Admin;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteSqlConsoleService : ISqlConsoleService
{
    private readonly ILocalDatabasePath _paths;
    private readonly ILocalDatabaseBootstrapper _bootstrapper;

    public SqliteSqlConsoleService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
    {
        _paths = paths;
        _bootstrapper = bootstrapper;
    }

    public async Task<SqlQueryResult> ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return new SqlQueryResult(false, "Введите SQL-запрос.", [], [], null, null);

        try
        {
            var trimmed = sql.Trim();
            var returnsRows = trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("EXPLAIN", StringComparison.OrdinalIgnoreCase);

            await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = trimmed;

            if (returnsRows)
            {
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                var columns = new List<string>();
                for (var i = 0; i < reader.FieldCount; i++)
                    columns.Add(reader.GetName(i));

                var rows = new List<IReadOnlyList<string?>>();
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var row = new string?[reader.FieldCount];
                    for (var i = 0; i < reader.FieldCount; i++)
                        row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
                    rows.Add(row);
                }

                return new SqlQueryResult(true, null, columns, rows, null, $"Строк: {rows.Count}");
            }

            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqlQueryResult(true, null, [], [], affected, $"Запрос выполнен. Затронуто строк: {affected}");
        }
        catch (Exception ex)
        {
            return new SqlQueryResult(false, ex.Message, [], [], null, null);
        }
    }
}
