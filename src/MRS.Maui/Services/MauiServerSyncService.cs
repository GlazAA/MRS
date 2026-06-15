using MRS.Application.Sync;
using MRS.Infrastructure.Sqlite;

namespace MRS.Maui.Services;

/// <summary>
/// Ручная синхронизация: сеть проверяется только по запросу пользователя.
/// Выгрузка в PostgreSQL будет подключена позже.
/// </summary>
public sealed class MauiServerSyncService : IServerSyncService
{
	private readonly ISyncOutboxQueryService _outbox;

	public MauiServerSyncService(ISyncOutboxQueryService outbox)
	{
		_outbox = outbox;
	}

	public ServerSyncResult? LastResult { get; private set; }

	public event Action? Changed;

	public async Task<ServerSyncResult> SyncAsync(CancellationToken cancellationToken = default)
	{
		var networkAvailable = IsNetworkAvailable();
		var pending = await _outbox.CountPendingAsync(cancellationToken).ConfigureAwait(false);

		ServerSyncResult result;
		if (!networkAvailable)
		{
			result = new ServerSyncResult(
				false,
				false,
				pending > 0
					? $"Сеть недоступна. В очереди на отправку: {pending} — повторите, когда появится интернет."
					: "Сеть недоступна. Подключитесь к интернету и нажмите снова.",
				pending,
				DateTimeOffset.Now);
		}
		else if (pending == 0)
		{
			result = new ServerSyncResult(
				true,
				true,
				"Сеть доступна. Локальные данные актуальны, очередь отправки пуста.",
				0,
				DateTimeOffset.Now);
		}
		else
		{
			// Заготовка: полная синхронизация с PostgreSQL подключится здесь.
			result = new ServerSyncResult(
				true,
				true,
				$"Сеть доступна. Готово к обмену с сервером: в очереди {pending} записей. " +
				"Полная синхронизация с PostgreSQL будет выполняться из этого пункта меню.",
				pending,
				DateTimeOffset.Now);
		}

		LastResult = result;
		Changed?.Invoke();
		return result;
	}

	private static bool IsNetworkAvailable()
	{
#if ANDROID || IOS || MACCATALYST || WINDOWS
		return Microsoft.Maui.Networking.Connectivity.NetworkAccess == Microsoft.Maui.Networking.NetworkAccess.Internet;
#else
		return false;
#endif
	}
}
