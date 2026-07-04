using MRS.Application.Checklists;
using MRS.Application.Storage;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

public class ChecklistEditValidateTests
{
	[Fact]
	public async Task GetForEdit_unit_number_is_editable()
	{
		var path = CreateTempDbPath();
		try
		{
			var edit = await CreateEditServiceAsync(path);
			var checklistId = await edit.BeginInProgressAsync(new BeginInProgressChecklistRequest(1, 1, 1, 1, DateTimeOffset.Now));

			var model = await edit.GetForEditAsync(checklistId);
			var unitField = model.Fields.First(f =>
				string.Equals(f.FieldCode, "unit_number", StringComparison.OrdinalIgnoreCase));

			Assert.False(unitField.IsLocked);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task GetForEdit_timing_fields_are_locked()
	{
		var path = CreateTempDbPath();
		try
		{
			var edit = await CreateEditServiceAsync(path);
			var checklistId = await edit.BeginInProgressAsync(new BeginInProgressChecklistRequest(1, 1, 1, 1, DateTimeOffset.Now));

			var model = await edit.GetForEditAsync(checklistId);
			foreach (var code in new[] { "start_date", "start_time", "end_date" })
			{
				var field = model.Fields.First(f =>
					string.Equals(f.FieldCode, code, StringComparison.OrdinalIgnoreCase));
				Assert.True(field.IsLocked, $"{code} should be locked");
			}
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task ValidateAsync_ignores_unchanged_fields()
	{
		var path = CreateTempDbPath();
		try
		{
			var edit = await CreateEditServiceAsync(path);
			var checklistId = await edit.BeginInProgressAsync(new BeginInProgressChecklistRequest(1, 1, 1, 1, DateTimeOffset.Now));
			var model = await edit.GetForEditAsync(checklistId);
			var values = model.Fields.ToDictionary(f => f.TemplateItemId, f => f.ValueRaw);

			var result = await edit.ValidateAsync(new UpdateChecklistAnswersRequest(checklistId, values));

			Assert.True(result.AllFieldsCanBeSaved);
			Assert.Empty(result.CanSaveFields);
			Assert.Empty(result.CannotSaveFields);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task ValidateAsync_editable_text_change_can_be_saved()
	{
		var path = CreateTempDbPath();
		try
		{
			var edit = await CreateEditServiceAsync(path);
			var checklistId = await edit.BeginInProgressAsync(new BeginInProgressChecklistRequest(1, 1, 1, 1, DateTimeOffset.Now));
			var model = await edit.GetForEditAsync(checklistId);
			var unitField = model.Fields.First(f =>
				string.Equals(f.FieldCode, "unit_number", StringComparison.OrdinalIgnoreCase));

			var values = model.Fields.ToDictionary(f => f.TemplateItemId, f => f.ValueRaw);
			values[unitField.TemplateItemId] = "У-42";

			var result = await edit.ValidateAsync(new UpdateChecklistAnswersRequest(checklistId, values));

			Assert.True(result.AllFieldsCanBeSaved);
			Assert.Single(result.CanSaveFields);
			Assert.Equal(unitField.TemplateItemId, result.CanSaveFields[0].TemplateItemId);
			Assert.Equal("У-42", result.CanSaveFields[0].AttemptedValue);
			Assert.Empty(result.CannotSaveFields);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task ValidateAsync_locked_timing_change_goes_to_cannot_save()
	{
		var path = CreateTempDbPath();
		try
		{
			var edit = await CreateEditServiceAsync(path);
			var checklistId = await edit.BeginInProgressAsync(new BeginInProgressChecklistRequest(1, 1, 1, 1, DateTimeOffset.Now));
			var model = await edit.GetForEditAsync(checklistId);
			var startDate = model.Fields.First(f =>
				string.Equals(f.FieldCode, "start_date", StringComparison.OrdinalIgnoreCase));

			var values = model.Fields.ToDictionary(f => f.TemplateItemId, f => f.ValueRaw);
			values[startDate.TemplateItemId] = "2020-01-01";

			var result = await edit.ValidateAsync(new UpdateChecklistAnswersRequest(checklistId, values));

			Assert.False(result.AllFieldsCanBeSaved);
			Assert.Empty(result.CanSaveFields);
			Assert.Single(result.CannotSaveFields);
			Assert.Equal(startDate.TemplateItemId, result.CannotSaveFields[0].TemplateItemId);
			Assert.Contains("учётом", result.CannotSaveFields[0].Reason!, StringComparison.OrdinalIgnoreCase);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task ValidateAsync_mixed_changes_splits_can_and_cannot_save()
	{
		var path = CreateTempDbPath();
		try
		{
			var edit = await CreateEditServiceAsync(path);
			var checklistId = await edit.BeginInProgressAsync(new BeginInProgressChecklistRequest(1, 1, 1, 1, DateTimeOffset.Now));
			var model = await edit.GetForEditAsync(checklistId);
			var unitField = model.Fields.First(f =>
				string.Equals(f.FieldCode, "unit_number", StringComparison.OrdinalIgnoreCase));
			var startDate = model.Fields.First(f =>
				string.Equals(f.FieldCode, "start_date", StringComparison.OrdinalIgnoreCase));

			var values = model.Fields.ToDictionary(f => f.TemplateItemId, f => f.ValueRaw);
			values[unitField.TemplateItemId] = "У-42";
			values[startDate.TemplateItemId] = "2020-01-01";

			var result = await edit.ValidateAsync(new UpdateChecklistAnswersRequest(checklistId, values));

			Assert.False(result.AllFieldsCanBeSaved);
			Assert.Single(result.CanSaveFields);
			Assert.Equal(unitField.TemplateItemId, result.CanSaveFields[0].TemplateItemId);
			Assert.Single(result.CannotSaveFields);
			Assert.Equal(startDate.TemplateItemId, result.CannotSaveFields[0].TemplateItemId);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public void ConflictReport_includes_engineer_and_blocked_fields()
	{
		var info = new ChecklistEditInfo(
			7, 1, null, null, "ООО Тест", "Объект 1", "Компрессор", 1, "1", "ЕЖН", "completed", 1);
		var blocked = new List<ChecklistUpdateDryRunField>
		{
			new(5001, "Дата начала", "start_date", "Управляется учётом времени работы", "2020-01-01")
		};

		var text = ChecklistEditConflictReport.FormatForAdmin(info, "Иванов И.И.", [], blocked);

		Assert.Contains("Иванов И.И.", text);
		Assert.Contains("КЛ №7", text);
		Assert.Contains("Дата начала", text);
		Assert.Contains("start_date", text);
	}

	private static string CreateTempDbPath() =>
		Path.Combine(Path.GetTempPath(), $"mrs_edit_{Guid.NewGuid():N}.db");

	private static async Task<SqliteChecklistEditService> CreateEditServiceAsync(string path)
	{
		var bootstrapper = new SqliteDatabaseBootstrapper();
		Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
		return new SqliteChecklistEditService(new FixedDbPath(path), bootstrapper);
	}

	private static void Cleanup(string path)
	{
		Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
		if (File.Exists(path))
			File.Delete(path);
	}

	private sealed class FixedDbPath(string path) : ILocalDatabasePath
	{
		public string GetDatabaseFilePath() => path;
	}
}
