using MRS.Application.Storage;
using MRS.Application.Visits;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

public class ScheduledVisitServiceTests
{
	[Fact]
	public async Task Create_visit_appears_on_calendar()
	{
		var path = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var paths = new FixedDbPath(path);
			var visits = new SqliteScheduledVisitService(paths, bootstrapper);

			var today = DateOnly.FromDateTime(DateTime.Today);
			var id = await visits.CreateAsync(new CreateScheduledVisitRequest(
				1, 1, null, today, today.AddDays(2), [1], null));

			await visits.SetPrepSkippedAsync(id, true);

			var month = await visits.GetCalendarMonthAsync(today.Year, today.Month);
			Assert.Contains(month, x => x.VisitId == id);
		}
		finally
		{
			Cleanup(path);
		}
	}

	[Fact]
	public async Task Contact_search_finds_by_phone_fragment()
	{
		var path = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			var paths = new FixedDbPath(path);
			var contacts = new SqliteOrganizationEmployeeQueryService(paths, bootstrapper);

			var results = await contacts.SearchAsync(1, "1112233");
			Assert.NotEmpty(results);
			Assert.Contains(results, r => r.DisplayName.Contains("Петров", StringComparison.Ordinal));
		}
		finally
		{
			Cleanup(path);
		}
	}

	private static string CreateTempDbPath() =>
		Path.Combine(Path.GetTempPath(), $"mrs_visit_{Guid.NewGuid():N}.db");

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
