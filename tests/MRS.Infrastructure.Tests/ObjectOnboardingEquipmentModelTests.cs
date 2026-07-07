using Microsoft.Data.Sqlite;
using MRS.Application.Facilities;
using MRS.Application.Storage;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

public class ObjectOnboardingEquipmentModelTests
{
	[Fact]
	public async Task UpsertHierarchyAsync_creates_new_equipment_type_with_manufacturer_and_model_in_one_transaction()
	{
		var path = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var paths = new FixedDbPath(path);
			var catalog = new SqliteEquipmentModelCatalogService(paths, bootstrapper);
			var onboarding = new SqliteObjectOnboardingService(paths, bootstrapper, catalog, new NoOpSyncOutboxService());

			var result = await onboarding.UpsertHierarchyAsync(new ObjectOnboardingRequest(
				ExistingOrganizationId: 1,
				NewOrganizationLegalFormCode: null,
				NewOrganizationCompanyName: null,
				ExistingFacilityId: 1,
				NewFacilityName: null,
				ContractAddress: null,
				AddressCity: null,
				AddressStreet: null,
				AddressBuilding: null,
				AddressStructure: null,
				AddressBlock: null,
				AddressZipCode: null,
				SystemDescription: null,
				ExistingEquipmentTypeId: null,
				NewEquipmentTypeName: "Тестовый компрессор",
				InstallationLabel: "ПН-001",
				InstallationManufacturer: "Atlas Copco",
				InstallationModel: "GA-37",
				InstallationSerialNumber: "SN-123"));

			Assert.True(result.EquipmentTypeCreated);
			Assert.True(result.InstallationCreated);

			var model = await catalog.GetInstallationModelAsync(result.InstallationId);
			Assert.NotNull(model);
			Assert.Equal("Atlas Copco", model!.Manufacturer);
			Assert.Equal("GA-37", model.ModelName);
			Assert.NotNull(model.EquipmentModelId);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task CreateBackupAsync_produces_valid_sqlite_file()
	{
		var path = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var paths = new FixedDbPath(path);
			var backup = new SqliteLocalDatabaseBackupService(paths, bootstrapper);

			var file = await backup.CreateBackupAsync();
			Assert.StartsWith("mrs-backup-", file.FileName, StringComparison.Ordinal);
			Assert.True(file.Content.Length > 100);
			Assert.Equal((byte)'S', file.Content[0]);
		}
		finally
		{
			Cleanup(path);
		}
	}

	private static string CreateTempDbPath() =>
		Path.Combine(Path.GetTempPath(), $"mrs_eq_{Guid.NewGuid():N}.db");

	private static void Cleanup(string path)
	{
		SqliteConnection.ClearAllPools();
		if (File.Exists(path))
			File.Delete(path);
	}

	private sealed class FixedDbPath(string path) : ILocalDatabasePath
	{
		public string GetDatabaseFilePath() => path;
	}
}
