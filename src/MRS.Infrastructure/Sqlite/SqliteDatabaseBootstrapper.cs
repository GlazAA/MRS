using Microsoft.Data.Sqlite;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteDatabaseBootstrapper : ILocalDatabaseBootstrapper
{
	public const int CurrentSchemaVersion = 6;

	private const string SchemaResourceName = "MRS.Infrastructure.Sqlite.Schema.sql";
	private const string SeedResourceName = "MRS.Infrastructure.Sqlite.Seed.sql";
	private const string DemoResourceName = "MRS.Infrastructure.Sqlite.Demo.sql";
	private const string DemoChecklistsResourceName = "MRS.Infrastructure.Sqlite.DemoChecklists.sql";
	private const string EquipmentFullResourceName = "MRS.Infrastructure.Sqlite.EquipmentFull.sql";
	private const string TemplatesDemoResourceName = "MRS.Infrastructure.Sqlite.TemplatesDemo.sql";
	private const string MosarchiveTemplatesResourceName = "MRS.Infrastructure.Sqlite.MosarchiveTemplates.sql";

	public async Task<LocalDatabaseStatus> EnsureReadyAsync(string databaseFilePath, CancellationToken cancellationToken = default)
	{
		try
		{
			var dir = Path.GetDirectoryName(databaseFilePath);
			if (!string.IsNullOrEmpty(dir))
				Directory.CreateDirectory(dir);

			var connectionString = new SqliteConnectionStringBuilder
			{
				DataSource = databaseFilePath,
				Mode = SqliteOpenMode.ReadWriteCreate,
				Cache = SqliteCacheMode.Shared,
				ForeignKeys = true
			}.ToString();

			await using var connection = new SqliteConnection(connectionString);
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			var version = await ReadUserVersionAsync(connection, cancellationToken).ConfigureAwait(false);
			if (version < 1)
			{
				var schema = await ReadEmbeddedResourceAsync(SchemaResourceName, cancellationToken).ConfigureAwait(false);
				var seed = await ReadEmbeddedResourceAsync(SeedResourceName, cancellationToken).ConfigureAwait(false);
				await SqliteScriptRunner.ExecuteScriptAsync(connection, schema, cancellationToken).ConfigureAwait(false);
				await SqliteScriptRunner.ExecuteScriptAsync(connection, seed, cancellationToken).ConfigureAwait(false);
				await WriteUserVersionAsync(connection, 1, cancellationToken).ConfigureAwait(false);
				version = 1;
			}

			if (version < 2)
			{
				var demo = await ReadEmbeddedResourceAsync(DemoResourceName, cancellationToken).ConfigureAwait(false);
				await SqliteScriptRunner.ExecuteScriptAsync(connection, demo, cancellationToken).ConfigureAwait(false);
				await WriteUserVersionAsync(connection, 2, cancellationToken).ConfigureAwait(false);
				version = 2;
			}

			if (version < 3)
			{
				var demoChecklists = await ReadEmbeddedResourceAsync(DemoChecklistsResourceName, cancellationToken).ConfigureAwait(false);
				await SqliteScriptRunner.ExecuteScriptAsync(connection, demoChecklists, cancellationToken).ConfigureAwait(false);
				await WriteUserVersionAsync(connection, 3, cancellationToken).ConfigureAwait(false);
				version = 3;
			}

			if (version < 4)
			{
				var fullEquipment = await ReadEmbeddedResourceAsync(EquipmentFullResourceName, cancellationToken).ConfigureAwait(false);
				await SqliteScriptRunner.ExecuteScriptAsync(connection, fullEquipment, cancellationToken).ConfigureAwait(false);
				await WriteUserVersionAsync(connection, 4, cancellationToken).ConfigureAwait(false);
				version = 4;
			}

			if (version < 5)
			{
				var templates = await ReadEmbeddedResourceAsync(TemplatesDemoResourceName, cancellationToken).ConfigureAwait(false);
				await SqliteScriptRunner.ExecuteScriptAsync(connection, templates, cancellationToken).ConfigureAwait(false);
				await WriteUserVersionAsync(connection, 5, cancellationToken).ConfigureAwait(false);
				version = 5;
			}

			if (version < 6)
			{
				var mos = await ReadEmbeddedResourceAsync(MosarchiveTemplatesResourceName, cancellationToken).ConfigureAwait(false);
				await SqliteScriptRunner.ExecuteScriptAsync(connection, mos, cancellationToken).ConfigureAwait(false);
				await WriteUserVersionAsync(connection, 6, cancellationToken).ConfigureAwait(false);
				version = 6;
			}

			var fieldTypes = await ScalarIntAsync(connection, "SELECT COUNT(*) FROM field_types;", cancellationToken).ConfigureAwait(false);
			var maintenanceTypes = await ScalarIntAsync(connection, "SELECT COUNT(*) FROM maintenance_types;", cancellationToken).ConfigureAwait(false);

			return new LocalDatabaseStatus(true, version, fieldTypes, maintenanceTypes, null);
		}
		catch (Exception ex)
		{
			return new LocalDatabaseStatus(false, 0, 0, 0, ex.Message);
		}
	}

	private static async Task<int> ReadUserVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "PRAGMA user_version;";
		var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is long l ? (int)l : Convert.ToInt32(scalar ?? 0);
	}

	private static async Task WriteUserVersionAsync(SqliteConnection connection, int version, CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = $"PRAGMA user_version = {version};";
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task<int> ScalarIntAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = sql;
		var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is long l ? (int)l : Convert.ToInt32(scalar ?? 0);
	}

	private static async Task<string> ReadEmbeddedResourceAsync(string name, CancellationToken cancellationToken)
	{
		var assembly = typeof(SqliteDatabaseBootstrapper).Assembly;
		await using var stream = assembly.GetManifestResourceStream(name)
			?? throw new InvalidOperationException($"Встроенный ресурс не найден: {name}. Доступно: {string.Join(", ", assembly.GetManifestResourceNames())}");
		using var reader = new StreamReader(stream);
		return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
	}
}
