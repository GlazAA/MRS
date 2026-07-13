using Npgsql;

namespace MRS.Infrastructure.Postgres;

internal static class PostgresSyncIdentityGuard
{
	internal static async Task EnsureNoConflictAsync(
		NpgsqlConnection connection,
		NpgsqlTransaction tx,
		string table,
		int id,
		string identityColumn,
		string identityValue,
		CancellationToken cancellationToken)
	{
		await using var cmd = new NpgsqlCommand($"SELECT {identityColumn} FROM {table} WHERE id = @id LIMIT 1;", connection, tx);
		cmd.Parameters.AddWithValue("id", id);
		var existing = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		if (existing is null or DBNull)
			return;

		var existingText = Convert.ToString(existing) ?? string.Empty;
		if (!string.Equals(existingText.Trim(), identityValue.Trim(), StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"CONFLICT: {table} id={id} уже содержит другие данные («{existingText}» ≠ «{identityValue}»).");
		}
	}
}
