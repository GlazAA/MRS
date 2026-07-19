using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

public class EquipmentModelCatalogTests
{
	[Fact]
	public async Task EnsureModelEntryAsync_is_case_insensitive_and_reuses_canonical_manufacturer()
	{
		var path = Path.Combine(Path.GetTempPath(), $"mrs_catalog_{Guid.NewGuid():N}.db");
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var catalog = new SqliteEquipmentModelCatalogService(new FixedDbPath(path), bootstrapper);

			var first = await catalog.EnsureModelEntryAsync(1, "Atlas Copco", "GA-37");
			var second = await catalog.EnsureModelEntryAsync(1, "atlas copco", "ga-37");
			var third = await catalog.EnsureModelEntryAsync(1, "ATLAS COPCO", "GA-55");

			Assert.Equal(first.Id, second.Id);
			Assert.Equal("Atlas Copco", second.Manufacturer);
			Assert.Equal("GA-37", second.Name);
			Assert.Equal("Atlas Copco", third.Manufacturer);
			Assert.NotEqual(first.Id, third.Id);

			var manufacturers = await catalog.GetManufacturersAsync(1);
			Assert.Equal(1, manufacturers.Count(m => string.Equals(m, "Atlas Copco", StringComparison.OrdinalIgnoreCase)));
		}
		finally
		{
			SqliteConnection.ClearAllPools();
			if (File.Exists(path))
				File.Delete(path);
		}
	}

	[Fact]
	public async Task GetManufacturersAsync_returns_global_handbook_across_equipment_types()
	{
		var path = Path.Combine(Path.GetTempPath(), $"mrs_catalog_global_{Guid.NewGuid():N}.db");
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var catalog = new SqliteEquipmentModelCatalogService(new FixedDbPath(path), bootstrapper);

			await catalog.EnsureModelEntryAsync(1, "Atlas Copco", "GA-37");
			await catalog.EnsureManufacturerAsync(2, "Siemens");

			var forMotor = await catalog.GetManufacturersAsync(2);
			Assert.Contains(forMotor, m => string.Equals(m, "Atlas Copco", StringComparison.OrdinalIgnoreCase));
			Assert.Contains(forMotor, m => string.Equals(m, "Siemens", StringComparison.OrdinalIgnoreCase));

			var models = await catalog.GetModelsAsync(2, "Atlas Copco");
			Assert.Contains(models, m => string.Equals(m.Name, "GA-37", StringComparison.OrdinalIgnoreCase));
		}
		finally
		{
			SqliteConnection.ClearAllPools();
			if (File.Exists(path))
				File.Delete(path);
		}
	}

	private sealed class FixedDbPath(string path) : ILocalDatabasePath
	{
		public string GetDatabaseFilePath() => path;
	}
}
