using Microsoft.Data.Sqlite;
using MRS.Application.Admin;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteAdminSupportRequestService : IAdminSupportRequestService
{
    private readonly ILocalDatabasePath _paths;
    private readonly ILocalDatabaseBootstrapper _bootstrapper;

    public SqliteAdminSupportRequestService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
    {
        _paths = paths;
        _bootstrapper = bootstrapper;
    }

    public async Task<int> SubmitAsync(int? authorUserId, string authorDisplayName, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Текст обращения не может быть пустым.", nameof(body));

        await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO admin_support_requests (author_user_id, author_display_name, body, status)
            VALUES ($uid, $name, $body, 'open');
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$uid", authorUserId.HasValue ? authorUserId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$name", authorDisplayName.Trim());
        cmd.Parameters.AddWithValue("$body", body.Trim());

        var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(scalar);
    }

    public async Task<IReadOnlyList<AdminSupportRequest>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, author_user_id, author_display_name, body, status, admin_reply, created_at, resolved_at
            FROM admin_support_requests
            ORDER BY
                CASE status WHEN 'open' THEN 0 ELSE 1 END,
                datetime(created_at) DESC;
            """;

        var list = new List<AdminSupportRequest>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new AdminSupportRequest(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return list;
    }

    public async Task ResolveAsync(int requestId, string? adminReply, CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE admin_support_requests
            SET status = 'resolved',
                admin_reply = $reply,
                resolved_at = datetime('now')
            WHERE id = $id AND status = 'open';
            """;
        cmd.Parameters.AddWithValue("$id", requestId);
        cmd.Parameters.AddWithValue("$reply", string.IsNullOrWhiteSpace(adminReply) ? DBNull.Value : adminReply.Trim());
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
