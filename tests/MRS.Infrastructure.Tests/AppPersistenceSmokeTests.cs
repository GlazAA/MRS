using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Facilities;
using MRS.Application.Notes;
using MRS.Application.Storage;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

/// <summary>Сквозные проверки сохранения и чтения: объект, КЛ (extra/remarks), фильтры заметок, бэкап БД.</summary>
public class AppPersistenceSmokeTests
{
	[Fact]
	public async Task Object_onboarding_persists_facility_installation_and_system_description()
	{
		var path = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var paths = new FixedDbPath(path);
			var onboarding = new SqliteObjectOnboardingService(
				paths, bootstrapper,
				new SqliteEquipmentModelCatalogService(paths, bootstrapper),
				new NoOpSyncOutboxService());
			var hierarchy = new SqliteFacilityHierarchyService(paths, bootstrapper);
			var installations = new SqliteInstallationQueryService(paths, bootstrapper);

			var result = await onboarding.UpsertHierarchyAsync(new ObjectOnboardingRequest(
				ExistingOrganizationId: null,
				NewOrganizationLegalFormCode: "OOO",
				NewOrganizationCompanyName: "Тест Фильтры",
				ExistingFacilityId: null,
				NewFacilityName: "Площадка Smoke",
				ContractAddress: "Курск, договор 1",
				AddressCity: "Курск",
				AddressStreet: "промзона",
				AddressBuilding: "1",
				AddressStructure: null,
				AddressBlock: null,
				AddressZipCode: "305000",
				SystemDescription: "3 установки, расходники FE65 за 14 дней",
				Installations:
				[
					new ObjectOnboardingInstallationDraft(1, null, "G301", "BOGE", "S-4", "SN-001")
				],
				Contacts: []));

			Assert.True(result.OrganizationCreated);
			Assert.True(result.FacilityCreated);
			Assert.True(result.InstallationsSaved >= 1);

			var facilities = await hierarchy.GetFacilitiesAsync(result.OrganizationId);
			Assert.Contains(facilities, f => f.Id == result.FacilityId && f.Name.Contains("Площадка Smoke", StringComparison.Ordinal));

			var systems = await hierarchy.GetSystemsAsync(result.FacilityId);
			var system = Assert.Single(systems);
			Assert.Contains("Гипоксическая", system.Name, StringComparison.Ordinal);

			var units = await installations.GetForSystemAndEquipmentAsync(system.Id, 1);
			Assert.Contains(units, u => string.Equals(u.Label, "G301", StringComparison.Ordinal));

			await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
			await connection.OpenAsync();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = "SELECT description FROM facility_systems WHERE id = $id;";
			cmd.Parameters.AddWithValue("$id", system.Id);
			var desc = (string?)await cmd.ExecuteScalarAsync();
			Assert.Equal("3 установки, расходники FE65 за 14 дней", desc);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task Checklist_extra_and_remarks_textarea_roundtrip_through_Apply_and_export()
	{
		var path = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var paths = new FixedDbPath(path);
			var edit = TestSyncServices.CreateEditService(paths, bootstrapper);
			var export = new SqliteChecklistDocumentExportService(paths, bootstrapper);

			var checklistId = await edit.BeginInProgressAsync(new BeginInProgressChecklistRequest(
				1, 1, 1, 1, DateTimeOffset.Now));

			var model = await edit.GetForEditAsync(checklistId);
			var extra = model.Fields.FirstOrDefault(f =>
				f.FieldCode is not null &&
				f.FieldCode.StartsWith("extra_", StringComparison.OrdinalIgnoreCase));
			var remarks = model.Fields.FirstOrDefault(f =>
				f.FieldCode is not null &&
				f.FieldCode.StartsWith("remarks_", StringComparison.OrdinalIgnoreCase));

			Assert.NotNull(extra);
			Assert.NotNull(remarks);
			Assert.Equal("textarea", extra!.FieldTypeName, ignoreCase: true);
			Assert.Equal("textarea", remarks!.FieldTypeName, ignoreCase: true);

			const string extraText = "Замена фильтров FE65-2P — 1 шт.";
			const string remarksText = "Требуется устранить негерметичности на трубопроводе.";

			var values = model.Fields.ToDictionary(f => f.TemplateItemId, f => f.ValueRaw);
			values[extra.TemplateItemId] = extraText;
			values[remarks.TemplateItemId] = remarksText;

			var apply = await edit.ApplyAsync(
				new UpdateChecklistAnswersRequest(checklistId, values),
				model.Fields.Select(f => f.TemplateItemId).ToList());
			Assert.True(apply.Ok, apply.ErrorMessage);

			var reloaded = await edit.GetForEditAsync(checklistId);
			Assert.Equal(extraText, reloaded.Fields.First(f => f.TemplateItemId == extra.TemplateItemId).ValueRaw);
			Assert.Equal(remarksText, reloaded.Fields.First(f => f.TemplateItemId == remarks.TemplateItemId).ValueRaw);

			var doc = await export.ExportDocAsync(checklistId);
			var html = System.Text.Encoding.UTF8.GetString(doc.Content);
			Assert.Contains(extraText, html, StringComparison.Ordinal);
			Assert.Contains(remarksText, html, StringComparison.Ordinal);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task Checklist_unit_number_is_hidden_on_fill_but_mirrored_text_saves()
	{
		var path = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var paths = new FixedDbPath(path);
			var edit = TestSyncServices.CreateEditService(paths, bootstrapper);
			var ensure = new SqliteInstallationEnsureService(paths, bootstrapper);

			var installationId = await ensure.EnsureAsync(1, 1, "G999");
			var checklistId = await edit.BeginInProgressAsync(new BeginInProgressChecklistRequest(
				installationId, 1, 1, 1, DateTimeOffset.Now));

			var model = await edit.GetForEditAsync(checklistId);
			var unit = Assert.Single(model.Fields, f => ChecklistFieldCodes.IsUnitNumber(f.FieldCode));
			Assert.True(ChecklistFieldCodes.IsHiddenOnFillForm(unit.FieldCode));

			var values = model.Fields.ToDictionary(f => f.TemplateItemId, f => f.ValueRaw);
			values[unit.TemplateItemId] = "G999";
			var apply = await edit.ApplyAsync(
				new UpdateChecklistAnswersRequest(checklistId, values),
				[unit.TemplateItemId]);
			Assert.True(apply.Ok, apply.ErrorMessage);

			var reloaded = await edit.GetForEditAsync(checklistId);
			Assert.Equal("G999", reloaded.Fields.First(f => f.TemplateItemId == unit.TemplateItemId).ValueRaw);
			Assert.Equal("G999", reloaded.Info.InstallationLabel);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task Engineer_note_filters_by_facility_visit_and_deadline_day()
	{
		var path = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var paths = new FixedDbPath(path);
			var notes = new SqliteEngineerNoteService(paths, bootstrapper, new NoOpSyncOutboxService());
			var visits = new SqliteScheduledVisitService(paths, bootstrapper, new NoOpSyncOutboxService());

			var day = new DateOnly(2026, 7, 20);
			var visitId = await visits.CreateAsync(new MRS.Application.Visits.CreateScheduledVisitRequest(
				1, 1, null, day, day, [1], null));

			var noteA = await notes.CreateAsync(new CreateEngineerNoteRequest(
				1, "Заметка A объект 1", day, "A", 1, visitId, null));
			var noteB = await notes.CreateAsync(new CreateEngineerNoteRequest(
				1, "Заметка B другой день", day.AddDays(1), "B", 1, null, null));
			_ = await notes.CreateAsync(new CreateEngineerNoteRequest(
				1, "Заметка C другой объект", day, "C", 2, null, null));

			var byFacility = await notes.ListAsync(new EngineerNoteFilter(1, null, null, null, null));
			Assert.Contains(byFacility, n => n.NoteId == noteA);
			Assert.Contains(byFacility, n => n.NoteId == noteB);
			Assert.DoesNotContain(byFacility, n => n.BodyPreview.Contains("другой объект", StringComparison.Ordinal));

			var byVisit = await notes.ListAsync(new EngineerNoteFilter(null, visitId, null, null, null));
			Assert.Single(byVisit);
			Assert.Equal(noteA, byVisit[0].NoteId);

			var byDay = await notes.ListAsync(new EngineerNoteFilter(null, null, null, null, day));
			Assert.Contains(byDay, n => n.NoteId == noteA);
			Assert.DoesNotContain(byDay, n => n.NoteId == noteB);

			var combined = await notes.ListAsync(new EngineerNoteFilter(1, visitId, null, null, day));
			Assert.Single(combined);
			Assert.Equal(noteA, combined[0].NoteId);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task Database_backup_contains_sqlite_header_and_roundtrips_data()
	{
		var path = CreateTempDbPath();
		var restorePath = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var paths = new FixedDbPath(path);
			var notes = new SqliteEngineerNoteService(paths, bootstrapper, new NoOpSyncOutboxService());
			await notes.CreateAsync(new CreateEngineerNoteRequest(
				1, "Маркер бэкапа XYZ-777", new DateOnly(2026, 8, 1), "BackupTest", 1, null, null));

			var backup = new SqliteLocalDatabaseBackupService(paths, bootstrapper);
			var file = await backup.CreateBackupAsync();
			Assert.EndsWith(".db", file.FileName, StringComparison.OrdinalIgnoreCase);
			Assert.True(file.Content.Length > 1000);
			Assert.Equal((byte)'S', file.Content[0]);
			Assert.Equal((byte)'Q', file.Content[1]);
			Assert.Equal((byte)'L', file.Content[2]);
			Assert.Equal((byte)'i', file.Content[3]);

			Assert.True((await bootstrapper.EnsureReadyAsync(restorePath)).Ready);
			var restorePaths = new FixedDbPath(restorePath);
			var restoreBackup = new SqliteLocalDatabaseBackupService(restorePaths, bootstrapper);
			await restoreBackup.RestoreFromBackupAsync(file.Content);

			var restoredNotes = new SqliteEngineerNoteService(restorePaths, bootstrapper, new NoOpSyncOutboxService());
			var list = await restoredNotes.ListAsync(new EngineerNoteFilter(null, null, null, null, null));
			Assert.Contains(list, n => n.BodyPreview.Contains("XYZ-777", StringComparison.Ordinal));
		}
		finally
		{
			Cleanup(path);
			Cleanup(restorePath);
		}
	}

	private static string CreateTempDbPath() =>
		Path.Combine(Path.GetTempPath(), $"mrs_smoke_{Guid.NewGuid():N}.db");

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
