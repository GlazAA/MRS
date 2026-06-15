using Microsoft.Data.Sqlite;
using MRS.Application.Facilities;
using MRS.Application.Storage;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

public class ObjectOnboardingOrganizationTests
{
	[Fact]
	public async Task UpsertHierarchyAsync_creates_organization_with_legal_form_and_company_name()
	{
		var path = CreateTempDbPath();
		try
		{
			var onboarding = await CreateOnboardingAsync(path);
			var hierarchy = new SqliteFacilityHierarchyService(new FixedDbPath(path), new SqliteDatabaseBootstrapper());

			var result = await onboarding.UpsertHierarchyAsync(new ObjectOnboardingRequest(
				ExistingOrganizationId: null,
				NewOrganizationLegalFormCode: "OOO",
				NewOrganizationCompanyName: "Рога и копыта",
				ExistingFacilityId: null,
				NewFacilityName: "Склад",
				ContractAddress: "г. Москва, договорной адрес",
				AddressCity: "Москва",
				AddressStreet: "Ленина",
				AddressBuilding: "1",
				AddressStructure: null,
				AddressBlock: null,
				AddressZipCode: "101000",
				SystemDescription: null,
				ExistingEquipmentTypeId: 1,
				NewEquipmentTypeName: null,
				InstallationLabel: "1",
				InstallationModel: null,
				InstallationSerialNumber: null));

			Assert.True(result.OrganizationCreated);
			var orgs = await hierarchy.GetOrganizationsAsync();
			var created = Assert.Single(orgs, o => o.Id == result.OrganizationId);
			Assert.Equal("ООО Рога и копыта", created.Name);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task UpsertHierarchyAsync_requires_legal_form_for_new_organization()
	{
		var path = CreateTempDbPath();
		try
		{
			var onboarding = await CreateOnboardingAsync(path);
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => onboarding.UpsertHierarchyAsync(new ObjectOnboardingRequest(
				null, "", "Компания", null, "Объект", null, "Москва", "Ленина", "1", null, null, null, null,
				1, null, "1", null, null)));
			Assert.Contains("юридический статус", ex.Message, StringComparison.OrdinalIgnoreCase);
		}
		finally
		{
			Cleanup(path);
		}
	}

	private static string CreateTempDbPath() =>
		Path.Combine(Path.GetTempPath(), $"mrs_org_{Guid.NewGuid():N}.db");

	private static async Task<SqliteObjectOnboardingService> CreateOnboardingAsync(string path)
	{
		var bootstrapper = new SqliteDatabaseBootstrapper();
		Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
		return new SqliteObjectOnboardingService(new FixedDbPath(path), bootstrapper);
	}

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
