namespace MRS.Application.Admin;

public interface ISqlConsoleService
{
    Task<SqlQueryResult> ExecuteAsync(string sql, CancellationToken cancellationToken = default);
}
