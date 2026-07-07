using Npgsql;

namespace MRS.Infrastructure.Postgres;

public sealed class PostgresConnectionFactory
{
	private readonly string _connectionString;

	public PostgresConnectionFactory(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("Строка подключения PostgreSQL не задана.", nameof(connectionString));
		_connectionString = connectionString;
	}

	public async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken = default)
	{
		var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		return connection;
	}
}
