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

			var result = await onboarding.UpsertHierarchyAsync(BaseRequest(
				ExistingOrganizationId: null,
				NewOrganizationLegalFormCode: "OOO",
				NewOrganizationCompanyName: "Рога и копыта",
				ExistingFacilityId: null,
				NewFacilityName: "Склад",
				ExistingEquipmentTypeId: 1,
				NewEquipmentTypeName: null));

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
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => onboarding.UpsertHierarchyAsync(BaseRequest(
				ExistingOrganizationId: null,
				NewOrganizationLegalFormCode: "",
				NewOrganizationCompanyName: "Компания",
				ExistingFacilityId: null,
				NewFacilityName: "Объект",
				ExistingEquipmentTypeId: 1,
				NewEquipmentTypeName: null)));
			Assert.Contains("юридический статус", ex.Message, StringComparison.OrdinalIgnoreCase);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task UpsertHierarchyAsync_saves_contact_with_separate_fio_linked_to_facility()
	{
		var path = CreateTempDbPath();
		try
		{
			var onboarding = await CreateOnboardingAsync(path);
			var result = await onboarding.UpsertHierarchyAsync(BaseRequest(
				ExistingOrganizationId: 1,
				NewOrganizationLegalFormCode: null,
				NewOrganizationCompanyName: null,
				ExistingFacilityId: 1,
				NewFacilityName: null,
				ExistingEquipmentTypeId: 1,
				NewEquipmentTypeName: null,
				contacts:
				[
					new ObjectOnboardingContactDraft("Иванов", "Пётр", "Сергеевич", "Инженер", "+79990001122", "p@test.ru")
				]));

			Assert.Equal(1, result.ContactsSaved);

			await using var connection = new SqliteConnection($"Data Source={path}");
			await connection.OpenAsync();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = """
				SELECT last_name, first_name, middle_name, facility_id, organization_id
				FROM organization_employees
				WHERE last_name = 'Иванов' AND first_name = 'Пётр';
				""";
			await using var reader = await cmd.ExecuteReaderAsync();
			Assert.True(await reader.ReadAsync());
			Assert.Equal("Иванов", reader.GetString(0));
			Assert.Equal("Пётр", reader.GetString(1));
			Assert.Equal("Сергеевич", reader.GetString(2));
			Assert.Equal(result.FacilityId, reader.GetInt32(3));
			Assert.Equal(result.OrganizationId, reader.GetInt32(4));
		}
		finally
		{
			Cleanup(path);
		}
	}

	private static ObjectOnboardingRequest BaseRequest(
		int? ExistingOrganizationId,
		string? NewOrganizationLegalFormCode,
		string? NewOrganizationCompanyName,
		int? ExistingFacilityId,
		string? NewFacilityName,
		int? ExistingEquipmentTypeId,
		string? NewEquipmentTypeName,
		IReadOnlyList<ObjectOnboardingContactDraft>? contacts = null) =>
		new(
			ExistingOrganizationId,
			NewOrganizationLegalFormCode,
			NewOrganizationCompanyName,
			ExistingFacilityId,
			NewFacilityName,
			ContractAddress: ExistingFacilityId is null ? "договор" : null,
			AddressCity: ExistingFacilityId is null ? "Москва" : null,
			AddressStreet: ExistingFacilityId is null ? "Ленина" : null,
			AddressBuilding: ExistingFacilityId is null ? "1" : null,
			AddressStructure: null,
			AddressBlock: null,
			AddressZipCode: ExistingFacilityId is null ? "101000" : null,
			SystemDescription: null,
			Installations:
			[
				new ObjectOnboardingInstallationDraft(
					ExistingEquipmentTypeId,
					NewEquipmentTypeName,
					"1",
					null,
					null,
					null)
			],
			Contacts: contacts ?? []);

	private static string CreateTempDbPath() =>
		Path.Combine(Path.GetTempPath(), $"mrs_org_{Guid.NewGuid():N}.db");

	private static async Task<SqliteObjectOnboardingService> CreateOnboardingAsync(string path)
	{
		var bootstrapper = new SqliteDatabaseBootstrapper();
		Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
		return new SqliteObjectOnboardingService(
			new FixedDbPath(path),
			bootstrapper,
			new SqliteEquipmentModelCatalogService(new FixedDbPath(path), bootstrapper),
			new NoOpSyncOutboxService());
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
