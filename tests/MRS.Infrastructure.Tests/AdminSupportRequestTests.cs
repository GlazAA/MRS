using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

public class AdminSupportRequestTests
{
	[Fact]
	public async Task SubmitAsync_persists_request_with_engineer_name_for_admin()
	{
		var path = CreateTempDbPath();
		try
		{
			var bootstrapper = new SqliteDatabaseBootstrapper();
			Assert.True((await bootstrapper.EnsureReadyAsync(path)).Ready);
			await TestDemoOperationalSeed.EnsureAsync(path, bootstrapper);
			var svc = new SqliteAdminSupportRequestService(new FixedDbPath(path), bootstrapper);

			var id = await svc.SubmitAsync(1, "Иванов Иван", "Не сохраняется контрольный лист");
			Assert.True(id > 0);
			Assert.Equal(1, await svc.CountOpenAsync());

			var list = await svc.ListAsync();
			var req = Assert.Single(list, r => r.Id == id);
			Assert.Equal("open", req.Status);
			Assert.Equal("Иванов Иван", req.AuthorDisplayName);
			Assert.Equal("Не сохраняется контрольный лист", req.Body);
			Assert.Equal(1, req.AuthorUserId);

			await svc.ResolveAsync(id, "Проверьте миграцию БД");
			Assert.Equal(0, await svc.CountOpenAsync());

			var after = Assert.Single(await svc.ListAsync(), r => r.Id == id);
			Assert.Equal("resolved", after.Status);
			Assert.Equal("Проверьте миграцию БД", after.AdminReply);
		}
		finally
		{
			Cleanup(path);
		}
	}

	private static string CreateTempDbPath() =>
		Path.Combine(Path.GetTempPath(), $"mrs_support_{Guid.NewGuid():N}.db");

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
