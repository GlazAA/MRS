using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Storage;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

public class SqliteDatabaseBootstrapperTests
{
	[Fact]
	public async Task EnsureReadyAsync_creates_schema_seed_and_demo()
	{
		var path = Path.Combine(Path.GetTempPath(), $"mrs_test_{Guid.NewGuid():N}.db");
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			var status = await bootstrapper.EnsureReadyAsync(path);

			Assert.True(status.Ready, status.Error);
			Assert.Equal(SqliteDatabaseBootstrapper.CurrentSchemaVersion, status.SchemaVersion);
			Assert.Equal(11, status.FieldTypeCount);
			Assert.Equal(9, status.MaintenanceTypeCount);

			Assert.Equal(3, await CountAsync(path, "SELECT COUNT(*) FROM organizations WHERE is_active = 1;"));
			Assert.Equal(3, await CountAsync(path, "SELECT COUNT(*) FROM checklists WHERE is_active = 1;"));
			Assert.Equal(20, await CountAsync(path, "SELECT COUNT(*) FROM checklist_templates WHERE is_active = 1;"));
			Assert.Equal(39, await CountAsync(path, "SELECT COUNT(*) FROM system_equipment_types WHERE system_id IN (1,2,3);"));

			var second = await bootstrapper.EnsureReadyAsync(path);
			Assert.True(second.Ready, second.Error);
			Assert.Equal(11, second.FieldTypeCount);
		}
		finally
		{
			SqliteConnection.ClearAllPools();
			if (File.Exists(path))
				File.Delete(path);
		}
	}

	[Fact]
	public async Task FacilityHierarchy_returns_demo_tree()
	{
		var path = Path.Combine(Path.GetTempPath(), $"mrs_tree_{Guid.NewGuid():N}.db");
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var paths = new FixedDbPath(path);
			var svc = new SqliteFacilityHierarchyService(paths, bootstrapper);
			var orgs = await svc.GetOrganizationsAsync();
			Assert.Equal(3, orgs.Count);
			var mir = Assert.Single(orgs, o => o.Name == "Мираторг");
			var facilities = await svc.GetFacilitiesAsync(mir.Id);
			Assert.Contains(facilities, f => f.Name == "Курск");
			var kursk = Assert.Single(facilities, f => f.Name == "Курск");
			var systems = await svc.GetSystemsAsync(kursk.Id);
			Assert.NotEmpty(systems);
		}
		finally
		{
			SqliteConnection.ClearAllPools();
			if (File.Exists(path))
				File.Delete(path);
		}
	}

	[Fact]
	public async Task Demo_system_1_has_equipment_catalog_and_checklist_summaries()
	{
		var path = Path.Combine(Path.GetTempPath(), $"mrs_catalog_{Guid.NewGuid():N}.db");
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var paths = new FixedDbPath(path);
			var catalog = new SqliteEquipmentTypeCatalogService(paths, bootstrapper);
			var types = await catalog.GetForSystemAsync(1);
			Assert.Equal(13, types.Count);
			var summaries = new SqliteChecklistSummaryService(paths, bootstrapper);
			var rows = await summaries.GetForSystemAsync(1);
			Assert.Equal(2, rows.Count);
			Assert.Contains(rows, r => r.StatusCode == "completed");
			Assert.Contains(rows, r => r.StatusCode == "in_progress");
			Assert.DoesNotContain(rows, r => r.StatusCode == "draft");
		}
		finally
		{
			SqliteConnection.ClearAllPools();
			if (File.Exists(path))
				File.Delete(path);
		}
	}

	[Fact]
	public async Task Checklist_flow_compressor_eight_forks_motor_unified_and_template_fields()
	{
		var path = Path.Combine(Path.GetTempPath(), $"mrs_flow_{Guid.NewGuid():N}.db");
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var paths = new FixedDbPath(path);
			var flow = new SqliteChecklistFlowService(paths, bootstrapper);

			var compressor = await flow.GetMaintenanceForkAsync(1);
			Assert.Equal(8, compressor.Count);
			Assert.DoesNotContain(compressor, f => f.MaintenanceCode == "INT-UNIFIED");

			var motor = await flow.GetMaintenanceForkAsync(2);
			Assert.Single(motor);
			Assert.Equal("INT-UNIFIED", motor[0].MaintenanceCode);

			var fields = await flow.GetTemplateFieldsAsync(1);
			Assert.True(fields.Count >= 18, $"Expected weekly compressor template to have many fields, got {fields.Count}.");

			var tid = await flow.ResolveTemplateIdAsync(1, 1);
			Assert.Equal(1, tid);
		}
		finally
		{
			SqliteConnection.ClearAllPools();
			if (File.Exists(path))
				File.Delete(path);
		}
	}

	private static async Task<int> CountAsync(string dbPath, string sql)
	{
		var b = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly, ForeignKeys = true };
		await using var conn = new SqliteConnection(b.ToString());
		await conn.OpenAsync();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = sql;
		var scalar = await cmd.ExecuteScalarAsync();
		return scalar is long l ? (int)l : Convert.ToInt32(scalar ?? 0);
	}

	private sealed class FixedDbPath(string path) : ILocalDatabasePath
	{
		public string GetDatabaseFilePath() => path;
	}

	[Fact]
	public void SplitStatements_preserves_create_index_with_where()
	{
		var sql = """
			CREATE TABLE t (id INT);
			CREATE INDEX ix ON t (id) WHERE id IS NULL;
			""";
		var parts = SqliteScriptRunner.SplitStatements(sql);
		Assert.Equal(2, parts.Count);
		Assert.Contains("WHERE id IS NULL", parts[1], StringComparison.Ordinal);
	}
}
