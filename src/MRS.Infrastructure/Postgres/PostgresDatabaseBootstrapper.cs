using Npgsql;

namespace MRS.Infrastructure.Postgres;

public sealed class PostgresDatabaseBootstrapper
{
	public const int CurrentSchemaVersion = 5;

	private const string SchemaResource = "MRS.Infrastructure.Postgres.Schema.sql";
	private const string SeedResource = "MRS.Infrastructure.Postgres.Seed.sql";
	private const string AlignResource = "MRS.Infrastructure.Postgres.Align.sql";
	private const string DemoResource = "MRS.Infrastructure.Postgres.Demo.sql";
	private const string TemplateIntroResource = "MRS.Infrastructure.Postgres.TemplateIntro.sql";

	private readonly PostgresConnectionFactory _factory;

	public PostgresDatabaseBootstrapper(PostgresConnectionFactory factory)
	{
		_factory = factory;
	}

	public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
		await EnsureMigrationsTableAsync(connection, cancellationToken).ConfigureAwait(false);
		var version = await ReadVersionAsync(connection, cancellationToken).ConfigureAwait(false);

		if (version < 1)
		{
			if (await TableExistsAsync(connection, "user_roles", cancellationToken).ConfigureAwait(false))
			{
				// Предыдущий запуск создал схему, но не записал версию миграции.
				await WriteVersionAsync(connection, 1, cancellationToken).ConfigureAwait(false);
				version = 1;
			}
			else
			{
				await PostgresScriptRunner.ExecuteScriptAsync(connection, await ReadResourceAsync(SchemaResource, cancellationToken), cancellationToken)
					.ConfigureAwait(false);
				await PostgresScriptRunner.ExecuteScriptAsync(connection, await ReadResourceAsync(SeedResource, cancellationToken), cancellationToken)
					.ConfigureAwait(false);
				await WriteVersionAsync(connection, 1, cancellationToken).ConfigureAwait(false);
				version = 1;
			}
		}

		if (version < 2)
		{
			await PostgresScriptRunner.ExecuteScriptAsync(connection, await ReadResourceAsync(AlignResource, cancellationToken), cancellationToken)
				.ConfigureAwait(false);
			await WriteVersionAsync(connection, 2, cancellationToken).ConfigureAwait(false);
			version = 2;
		}

		if (version < 3)
		{
			await PostgresScriptRunner.ExecuteScriptAsync(connection, await ReadResourceAsync(DemoResource, cancellationToken), cancellationToken)
				.ConfigureAwait(false);
			await WriteVersionAsync(connection, 3, cancellationToken).ConfigureAwait(false);
			version = 3;
		}

		if (version < 4)
		{
			await PostgresUserSeeder.EnsureDemoUsersAsync(connection, cancellationToken).ConfigureAwait(false);
			await WriteVersionAsync(connection, 4, cancellationToken).ConfigureAwait(false);
			version = 4;
		}

		if (version < 5)
		{
			await PostgresScriptRunner.ExecuteScriptAsync(connection, await ReadResourceAsync(TemplateIntroResource, cancellationToken), cancellationToken)
				.ConfigureAwait(false);
			await WriteVersionAsync(connection, 5, cancellationToken).ConfigureAwait(false);
		}
	}

	private static async Task EnsureMigrationsTableAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		await using var cmd = new NpgsqlCommand("""
			CREATE TABLE IF NOT EXISTS schema_migrations (
			    version INT PRIMARY KEY,
			    applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
			);
			""", connection);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task<int> ReadVersionAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		await using var cmd = new NpgsqlCommand("SELECT COALESCE(MAX(version), 0) FROM schema_migrations;", connection);
		var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is int i ? i : Convert.ToInt32(scalar ?? 0);
	}

	private static async Task WriteVersionAsync(NpgsqlConnection connection, int version, CancellationToken cancellationToken)
	{
		await using var cmd = new NpgsqlCommand(
			"INSERT INTO schema_migrations (version) VALUES (@v) ON CONFLICT (version) DO NOTHING;",
			connection);
		cmd.Parameters.AddWithValue("v", version);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string tableName, CancellationToken cancellationToken)
	{
		await using var cmd = new NpgsqlCommand("""
			SELECT EXISTS (
			    SELECT 1 FROM information_schema.tables
			    WHERE table_schema = 'public' AND table_name = @name
			);
			""", connection);
		cmd.Parameters.AddWithValue("name", tableName);
		var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is true;
	}

	private static async Task<string> ReadResourceAsync(string name, CancellationToken cancellationToken)
	{
		var assembly = typeof(PostgresDatabaseBootstrapper).Assembly;
		await using var stream = assembly.GetManifestResourceStream(name)
			?? throw new InvalidOperationException($"Ресурс не найден: {name}");
		using var reader = new StreamReader(stream);
		return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
	}
}
