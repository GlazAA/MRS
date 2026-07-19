using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Storage;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

public class ChecklistWorkSessionTests
{
	[Fact]
	public async Task Work_session_tracks_pause_resume_and_complete()
	{
		var path = Path.Combine(Path.GetTempPath(), $"mrs_work_{Guid.NewGuid():N}.db");
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			await TestDemoOperationalSeed.EnsureAsync(path, bootstrapper);
			var paths = new FixedDbPath(path);
			var edit = TestSyncServices.CreateEditService(paths, bootstrapper);

			var workStartedAt = DateTimeOffset.Now.AddMinutes(-10);
			var checklistId = await edit.BeginInProgressAsync(new BeginInProgressChecklistRequest(
				InstallationId: 1,
				ChecklistTemplateId: 1,
				MaintenanceTypeId: 1,
				EngineerUserId: 1,
				WorkStartedAt: workStartedAt));

			var active = await edit.GetForEditAsync(checklistId);
			Assert.Equal("in_progress", active.Info.StatusCode);
			Assert.Null(active.Info.EndedAt);
			Assert.True(Math.Abs((active.Info.StartedAt!.Value - workStartedAt).TotalSeconds) < 1);

			await edit.PauseWorkAsync(checklistId);
			var paused = await edit.GetForEditAsync(checklistId);
			Assert.Equal("in_progress", paused.Info.StatusCode);
			Assert.NotNull(paused.Info.EndedAt);

			var pausedElapsed = paused.Info.EndedAt!.Value - paused.Info.StartedAt!.Value;
			Assert.True(pausedElapsed >= TimeSpan.FromMinutes(9));

			await Task.Delay(50);
			await edit.ResumeWorkAsync(checklistId);
			var resumed = await edit.GetForEditAsync(checklistId);
			Assert.Null(resumed.Info.EndedAt);
			Assert.True(resumed.Info.StartedAt!.Value < DateTimeOffset.Now.AddMinutes(-9));

			await edit.PauseWorkAsync(checklistId);
			await edit.CompleteWorkAsync(checklistId);
			var completed = await edit.GetForEditAsync(checklistId);
			Assert.Equal("completed", completed.Info.StatusCode);
			Assert.NotNull(completed.Info.EndedAt);
		}
		finally
		{
			SqliteConnection.ClearAllPools();
			if (File.Exists(path))
				File.Delete(path);
		}
	}

	[Fact]
	public async Task Summary_shows_elapsed_for_paused_in_progress()
	{
		var path = Path.Combine(Path.GetTempPath(), $"mrs_summary_{Guid.NewGuid():N}.db");
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			await TestDemoOperationalSeed.EnsureAsync(path, bootstrapper);
			var paths = new FixedDbPath(path);
			var edit = TestSyncServices.CreateEditService(paths, bootstrapper);
			var summaries = new SqliteChecklistSummaryService(paths, bootstrapper);

			var startedAt = DateTimeOffset.Now.AddMinutes(-5);
			var checklistId = await edit.BeginInProgressAsync(new BeginInProgressChecklistRequest(
				1, 1, 1, 1, startedAt));
			await edit.PauseWorkAsync(checklistId);

			var rows = await summaries.GetForSystemAsync(1);
			var row = Assert.Single(rows, r => r.ChecklistId == checklistId);
			Assert.Equal("in_progress", row.StatusCode);
			Assert.NotNull(row.EndedAt);

			var label = ChecklistDurationFormatter.Format(row.StartedAt, row.EndedAt);
			Assert.Matches(@"^\d+:\d{2}:\d{2}$", label);
			Assert.NotEqual("—", label);
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
