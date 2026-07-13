using MRS.Application.Storage;
using MRS.Application.Sync;

namespace MRS.Maui.Services;

/// <summary>
/// Однократный запуск при открытии приложения: локальная БД + тихая попытка обновить данные с сервера.
/// </summary>
public sealed class MauiAppStartupService
{
	private readonly ILocalDatabasePath _databasePath;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;
	private readonly IServerSyncService _sync;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private bool _completed;

	public MauiAppStartupService(
		ILocalDatabasePath databasePath,
		ILocalDatabaseBootstrapper bootstrapper,
		IServerSyncService sync)
	{
		_databasePath = databasePath;
		_bootstrapper = bootstrapper;
		_sync = sync;
	}

	public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
	{
		if (_completed)
			return;

		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_completed)
				return;

			var path = _databasePath.GetDatabaseFilePath();
			await _bootstrapper.EnsureReadyAsync(path, cancellationToken).ConfigureAwait(false);

			if (MauiSyncDefaults.ServerSyncEnabled)
				_ = await _sync.SyncAsync(cancellationToken).ConfigureAwait(false);

			_completed = true;
		}
		finally
		{
			_gate.Release();
		}
	}
}
