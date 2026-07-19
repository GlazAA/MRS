using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Storage;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

public class TemplateCloneAuthoringTests
{
	[Fact]
	public async Task List_and_clone_draft_skips_unit_number_and_preserves_field_codes()
	{
		var path = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			await TestDemoOperationalSeed.EnsureAsync(path, bootstrapper);
			var paths = new FixedDbPath(path);
			var authoring = new SqliteChecklistTemplateAuthoringService(paths, bootstrapper, new NoOpSyncOutboxService());

			var sources = await authoring.ListTemplatesForCloneAsync();
			Assert.Contains(sources, s => s.TemplateId == 1 && s.FieldCount >= 3);

			var draft = await authoring.GetTemplateCloneDraftAsync(1);
			Assert.Equal(1, draft.TemplateId);
			Assert.DoesNotContain(draft.Fields, f => ChecklistFieldCodes.IsUnitNumber(f.FieldCode));
			Assert.DoesNotContain(draft.Fields, f => ChecklistFieldCodes.IsEndTime(f.FieldCode));
			Assert.Contains(draft.Fields, f => f.FieldCode == "start_date");
			Assert.Contains(draft.Fields, f => f.FieldCode == "pressure_network");

			var kept = draft.Fields
				.Where(f => f.FieldCode is "start_date" or "start_time" or "pressure_network")
				.Select((f, i) => new CreateTemplateFieldRequest(
					SortOrder: i + 1,
					FieldCode: f.FieldCode,
					QuestionText: f.QuestionText,
					HintText: f.HintText,
					FieldTypeName: f.FieldTypeName,
					IsRequired: f.IsRequired,
					GroupName: null,
					ValidationRuleCode: null,
					Options: f.Options))
				.ToList();

			var newId = await authoring.CreateTemplateAsync(new CreateChecklistTemplateRequest(
				FacilityId: 1,
				EquipmentTypeId: 2,
				ExistingMaintenanceTypeId: 9,
				NewMaintenanceTypeName: null,
				NewMaintenanceTypeCode: null,
				NewMaintenanceTypeDescription: null,
				TemplateName: "Клон smoke — мотор",
				ScenarioCode: "SC-TEST-CLONE-MOTOR",
				TopPlateText: draft.TopPlateText,
				IntroModalText: null,
				SafetyModalText: draft.SafetyModalText,
				RedButtonEnabled: draft.RedButtonEnabled,
				Fields: kept));

			Assert.True(newId > 0);

			await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
			await connection.OpenAsync();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = """
				SELECT field_code, facility_id
				FROM checklist_template_items cti
				INNER JOIN checklist_templates ct ON ct.id = cti.checklist_template_id
				WHERE cti.checklist_template_id = $id
				ORDER BY cti.sort_order;
				""";
			cmd.Parameters.AddWithValue("$id", newId);
			var codes = new List<string>();
			int? facilityId = null;
			await using var reader = await cmd.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				codes.Add(reader.GetString(0));
				facilityId = reader.GetInt32(1);
			}

			Assert.Equal(["start_date", "start_time", "pressure_network"], codes);
			Assert.Equal(1, facilityId);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task Analogy_save_binds_to_facility_and_is_preferred_over_global_template()
	{
		var path = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			await TestDemoOperationalSeed.EnsureAsync(path, bootstrapper);
			Assert.Equal(SqliteDatabaseBootstrapper.CurrentSchemaVersion, (await bootstrapper.EnsureReadyAsync(path)).SchemaVersion);
			var paths = new FixedDbPath(path);
			var authoring = new SqliteChecklistTemplateAuthoringService(paths, bootstrapper, new NoOpSyncOutboxService());
			var flow = new SqliteChecklistFlowService(paths, bootstrapper);

			const int targetFacilityId = 2; // Курск
			const int targetEquipmentTypeId = 5; // фильтры
			const int targetMaintenanceTypeId = 9; // единое ТО (есть глобальный демо-шаблон)

			var draft = await authoring.GetTemplateCloneDraftAsync(1);
			Assert.Equal(1, draft.EquipmentTypeId);
			Assert.Equal(1, draft.MaintenanceTypeId);

			var fields = draft.Fields
				.Where(f => !string.Equals(f.FieldCode, "comments", StringComparison.OrdinalIgnoreCase))
				.Select((f, i) => new CreateTemplateFieldRequest(
					SortOrder: i + 1,
					FieldCode: f.FieldCode,
					QuestionText: f.QuestionText + " (клон)",
					HintText: f.HintText,
					FieldTypeName: f.FieldTypeName,
					IsRequired: f.IsRequired,
					GroupName: null,
					ValidationRuleCode: null,
					Options: f.Options))
				.ToList();
			Assert.True(fields.Count >= 2);

			var globalBefore = await flow.ResolveTemplateIdAsync(targetEquipmentTypeId, targetMaintenanceTypeId, targetFacilityId);
			Assert.NotNull(globalBefore); // демо-глобальный шаблон фильтров

			var newId = await authoring.CreateTemplateAsync(new CreateChecklistTemplateRequest(
				FacilityId: targetFacilityId,
				EquipmentTypeId: targetEquipmentTypeId,
				ExistingMaintenanceTypeId: targetMaintenanceTypeId,
				NewMaintenanceTypeName: null,
				NewMaintenanceTypeCode: null,
				NewMaintenanceTypeDescription: null,
				TemplateName: "Фильтры Курск — по аналогии",
				ScenarioCode: "SC-TEST-ANALOGY-FILTERS-KURSK",
				TopPlateText: "Плашка клона",
				IntroModalText: null,
				SafetyModalText: "Безопасность клона",
				RedButtonEnabled: true,
				Fields: fields));

			await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
			await connection.OpenAsync();

			using (var meta = connection.CreateCommand())
			{
				meta.CommandText = """
					SELECT facility_id, equipment_type_id, maintenance_type_id, template_name
					FROM checklist_templates
					WHERE id = $id;
					""";
				meta.Parameters.AddWithValue("$id", newId);
				await using var reader = await meta.ExecuteReaderAsync();
				Assert.True(await reader.ReadAsync());
				Assert.Equal(targetFacilityId, reader.GetInt32(0));
				Assert.Equal(targetEquipmentTypeId, reader.GetInt32(1));
				Assert.Equal(targetMaintenanceTypeId, reader.GetInt32(2));
				Assert.Equal("Фильтры Курск — по аналогии", reader.GetString(3));
			}

			var resolved = await flow.ResolveTemplateIdAsync(targetEquipmentTypeId, targetMaintenanceTypeId, targetFacilityId);
			Assert.Equal(newId, resolved);
			Assert.NotEqual(globalBefore, resolved);

			var otherFacility = await flow.ResolveTemplateIdAsync(targetEquipmentTypeId, targetMaintenanceTypeId, 1);
			Assert.Equal(globalBefore, otherFacility);

			var forks = await flow.GetMaintenanceForkAsync(targetEquipmentTypeId, targetFacilityId);
			Assert.Contains(forks, f => f.ChecklistTemplateId == newId && f.IsFacilitySpecific);
		}
		finally
		{
			Cleanup(path);
		}
	}

	private static string CreateTempDbPath() =>
		Path.Combine(Path.GetTempPath(), $"mrs-clone-{Guid.NewGuid():N}.db");

	private static void Cleanup(string path)
	{
		SqliteConnection.ClearAllPools();
		try
		{
			if (File.Exists(path))
				File.Delete(path);
		}
		catch
		{
			/* ignore */
		}
	}

	private sealed class FixedDbPath(string path) : ILocalDatabasePath
	{
		public string GetDatabaseFilePath() => path;
	}
}
