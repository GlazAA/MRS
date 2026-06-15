using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Storage;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

public class ChecklistWorkTimeIntegrationTests
{
	[Fact]
	public async Task BeginInProgress_stored_start_at_roundtrips_through_GetForEdit()
	{
		var path = CreateTempDbPath();
		try
		{
			var (edit, _) = await CreateServicesAsync(path);
			var workStartedAt = new DateTimeOffset(2026, 6, 14, 10, 0, 0, TimeSpan.FromHours(3));

			var checklistId = await edit.BeginInProgressAsync(new BeginInProgressChecklistRequest(
				1, 1, 1, 1, workStartedAt));

			var model = await edit.GetForEditAsync(checklistId);
			Assert.NotNull(model.Info.StartedAt);
			Assert.True(Math.Abs((model.Info.StartedAt!.Value - workStartedAt).TotalSeconds) < 1);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task ApplyAsync_with_start_date_does_not_reset_work_session_elapsed_time()
	{
		var path = CreateTempDbPath();
		try
		{
			var (edit, summaries) = await CreateServicesAsync(path);
			var workStartedAt = DateTimeOffset.Now.AddMinutes(-10);
			var checklistId = await edit.BeginInProgressAsync(new BeginInProgressChecklistRequest(
				1, 1, 1, 1, workStartedAt));

			var model = await edit.GetForEditAsync(checklistId);
			var startDateField = model.Fields.First(f =>
				string.Equals(f.FieldCode, "start_date", StringComparison.OrdinalIgnoreCase));

			var values = model.Fields.ToDictionary(f => f.TemplateItemId, f => f.ValueRaw);
			values[startDateField.TemplateItemId] = DateTimeOffset.Now.ToString("yyyy-MM-dd");

			var fieldIds = model.Fields.Select(f => f.TemplateItemId).ToList();
			var apply = await edit.ApplyAsync(new UpdateChecklistAnswersRequest(checklistId, values), fieldIds);
			Assert.True(apply.Ok, apply.ErrorMessage);

			await edit.PauseWorkAsync(checklistId);

			var paused = await edit.GetForEditAsync(checklistId);
			Assert.NotNull(paused.Info.StartedAt);
			Assert.NotNull(paused.Info.EndedAt);

			var elapsed = paused.Info.EndedAt!.Value - paused.Info.StartedAt!.Value;
			Assert.True(elapsed >= TimeSpan.FromMinutes(9),
				$"Expected >= 9 min, got {elapsed.TotalMinutes:F1} min (start={paused.Info.StartedAt:o}, end={paused.Info.EndedAt:o})");

			var row = Assert.Single(await summaries.GetForSystemAsync(1), r => r.ChecklistId == checklistId);
			var label = ChecklistDurationFormatter.Format(row.StartedAt, row.EndedAt);
			Assert.NotEqual("—", label);
			Assert.NotEqual("0:00:00", label);
			Assert.True(ChecklistDurationFormatter.Elapsed(row.StartedAt, row.EndedAt) >= TimeSpan.FromMinutes(9));
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task Real_delay_session_records_at_least_one_second()
	{
		var path = CreateTempDbPath();
		try
		{
			var (edit, summaries) = await CreateServicesAsync(path);
			var workStartedAt = DateTimeOffset.Now;
			var checklistId = await edit.BeginInProgressAsync(new BeginInProgressChecklistRequest(
				1, 1, 1, 1, workStartedAt));

			await Task.Delay(1100);

			await edit.PauseWorkAsync(checklistId);

			var row = Assert.Single(await summaries.GetForSystemAsync(1), r => r.ChecklistId == checklistId);
			var elapsed = ChecklistDurationFormatter.Elapsed(row.StartedAt, row.EndedAt);
			Assert.True(elapsed.TotalSeconds >= 1,
				$"Expected >= 1 sec, got {elapsed.TotalSeconds:F3}s");
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task Resume_accumulates_previous_session_time()
	{
		var path = CreateTempDbPath();
		try
		{
			var (edit, summaries) = await CreateServicesAsync(path);
			var firstStart = DateTimeOffset.Now.AddMinutes(-5);
			var checklistId = await edit.BeginInProgressAsync(new BeginInProgressChecklistRequest(
				1, 1, 1, 1, firstStart));
			await edit.PauseWorkAsync(checklistId);

			var afterFirstPause = await edit.GetForEditAsync(checklistId);
			var firstElapsed = afterFirstPause.Info.EndedAt!.Value - afterFirstPause.Info.StartedAt!.Value;
			Assert.True(firstElapsed >= TimeSpan.FromMinutes(4));

			await edit.ResumeWorkAsync(checklistId);
			await Task.Delay(1100);
			await edit.PauseWorkAsync(checklistId);

			var afterSecondPause = await edit.GetForEditAsync(checklistId);
			var totalElapsed = afterSecondPause.Info.EndedAt!.Value - afterSecondPause.Info.StartedAt!.Value;
			Assert.True(totalElapsed > firstElapsed,
				$"Expected total {totalElapsed.TotalSeconds:F1}s > first {firstElapsed.TotalSeconds:F1}s");

			var row = Assert.Single(await summaries.GetForSystemAsync(1), r => r.ChecklistId == checklistId);
			Assert.True(ChecklistDurationFormatter.Elapsed(row.StartedAt, row.EndedAt) >= TimeSpan.FromMinutes(4));
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public void FormatActive_counts_elapsed_from_start()
	{
		var start = DateTimeOffset.Now.AddMinutes(-2).AddSeconds(-5);
		var label = ChecklistDurationFormatter.FormatActive(start);
		Assert.Matches(@"^\d+:\d{2}:\d{2}$", label);
		Assert.NotEqual("0:00:00", label);
		Assert.True(ChecklistDurationFormatter.Elapsed(start, null, DateTimeOffset.Now) >= TimeSpan.FromMinutes(2));
	}

	private static string CreateTempDbPath() =>
		Path.Combine(Path.GetTempPath(), $"mrs_time_{Guid.NewGuid():N}.db");

	private static async Task<(SqliteChecklistEditService Edit, SqliteChecklistSummaryService Summaries)> CreateServicesAsync(string path)
	{
		var bootstrapper = new SqliteDatabaseBootstrapper();
		Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
		var paths = new FixedDbPath(path);
		return (new SqliteChecklistEditService(paths, bootstrapper), new SqliteChecklistSummaryService(paths, bootstrapper));
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
