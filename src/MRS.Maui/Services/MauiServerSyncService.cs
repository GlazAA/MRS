using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MRS.Application.Sync;

namespace MRS.Maui.Services;

public sealed class MauiServerSyncService : IServerSyncService
{
	private readonly ISyncOutboxService _outbox;
	private readonly IServerConnectionSettings _settings;
	private readonly MauiUserAuthService _auth;
	private readonly ISyncApplyService _apply;
	private readonly ISyncPushAckService _pushAck;
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

	public MauiServerSyncService(
		ISyncOutboxService outbox,
		IServerConnectionSettings settings,
		MauiUserAuthService auth,
		ISyncApplyService apply,
		ISyncPushAckService pushAck)
	{
		_outbox = outbox;
		_settings = settings;
		_auth = auth;
		_apply = apply;
		_pushAck = pushAck;
	}

	public ServerSyncResult? LastResult { get; private set; }

	public event Action? Changed;

	public async Task<ServerSyncResult> SyncAsync(CancellationToken cancellationToken = default)
	{
		var pending = await _outbox.CountPendingAsync(cancellationToken).ConfigureAwait(false);

		if (!MauiSyncDefaults.ServerSyncEnabled)
		{
			return Finish(new ServerSyncResult(
				false,
				false,
				"Синхронизация с сервером временно отключена.",
				pending,
				DateTimeOffset.Now));
		}

		var networkAvailable = IsNetworkAvailable();

		if (!networkAvailable)
		{
			return Finish(new ServerSyncResult(
				false,
				false,
				pending > 0
					? $"Сеть недоступна. Работаем с данными на устройстве. В очереди на отправку: {pending}."
					: "Сеть недоступна. Работаем с данными на устройстве.",
				pending,
				DateTimeOffset.Now));
		}

		for (var attempt = 0; attempt < 2; attempt++)
		{
			if (!await _auth.EnsureSyncAuthenticatedAsync(cancellationToken).ConfigureAwait(false))
			{
				return Finish(new ServerSyncResult(
					true,
					false,
					"Сервер недоступен. Работаем с последними данными на устройстве.",
					pending,
					DateTimeOffset.Now));
			}

			try
			{
				return await SyncWithServerAsync(pending, cancellationToken).ConfigureAwait(false);
			}
			catch (HttpRequestException ex) when (attempt == 0 && IsUnauthorized(ex))
			{
				_auth.ClearAccessToken();
			}
		}

		return Finish(new ServerSyncResult(
			true,
			false,
			"Сервер недоступен. Работаем с последними данными на устройстве.",
			pending,
			DateTimeOffset.Now));
	}

	private async Task<ServerSyncResult> SyncWithServerAsync(int pending, CancellationToken cancellationToken)
	{
		try
		{
			using var client = _auth.CreateAuthorizedClient();

			var pullRequest = new SyncPullRequest(_settings.LastPullAt);
			using var pullResponse = await client.PostAsJsonAsync("/api/sync/pull", pullRequest, cancellationToken)
				.ConfigureAwait(false);
			pullResponse.EnsureSuccessStatusCode();
			var pull = await pullResponse.Content.ReadFromJsonAsync<SyncPullResponse>(JsonOptions, cancellationToken)
				.ConfigureAwait(false);
			if (pull is not null)
			{
				await _apply.ApplyPullAsync(pull, cancellationToken).ConfigureAwait(false);
				_settings.LastPullAt = pull.ServerTime;
				if (_settings is MauiServerConnectionSettings mauiSettings)
					mauiSettings.Save();
			}

			var batch = await _outbox.GetPendingAsync(50, cancellationToken).ConfigureAwait(false);
			var pushed = 0;
			var failed = 0;
			if (batch.Count > 0)
			{
				var pushRequest = new SyncPushRequest(batch.Select(b => new SyncPushItem(
					b.Id, b.EntityType, b.LocalClientUuid, b.Operation, b.PayloadJson)).ToList());
				using var pushResponse = await client.PostAsJsonAsync("/api/sync/push", pushRequest, cancellationToken)
					.ConfigureAwait(false);
				pushResponse.EnsureSuccessStatusCode();
				var push = await pushResponse.Content.ReadFromJsonAsync<SyncPushResponse>(JsonOptions, cancellationToken)
					.ConfigureAwait(false);
				if (push is not null)
				{
					foreach (var item in push.Results)
					{
						if (item.Ok)
						{
							await _outbox.MarkProcessedAsync(item.OutboxId, cancellationToken).ConfigureAwait(false);
							await MarkEntitySyncedAsync(batch, item.OutboxId, cancellationToken).ConfigureAwait(false);
							pushed++;
						}
						else
						{
							await _outbox.MarkFailedAsync(item.OutboxId, item.Error ?? "Ошибка", cancellationToken).ConfigureAwait(false);
							failed++;
						}
					}
				}
			}

			pending = await _outbox.CountPendingAsync(cancellationToken).ConfigureAwait(false);
			var message = failed > 0
				? $"Синхронизация частично выполнена: отправлено {pushed}, ошибок {failed}, в очереди {pending}."
				: pending > 0
					? $"Справочники обновлены. В очереди ещё {pending} записей."
					: "Синхронизация выполнена успешно.";

			return Finish(new ServerSyncResult(true, failed == 0, message, pending, DateTimeOffset.Now));
		}
		catch (Exception ex)
		{
			return Finish(new ServerSyncResult(
				true,
				false,
				$"Сервер недоступен ({ex.Message}). Работаем с данными на устройстве.",
				pending,
				DateTimeOffset.Now));
		}
	}

	private static bool IsUnauthorized(HttpRequestException ex) =>
		ex.StatusCode == HttpStatusCode.Unauthorized;

	private async Task MarkEntitySyncedAsync(
		IReadOnlyList<SyncOutboxEntry> batch,
		long outboxId,
		CancellationToken cancellationToken)
	{
		var entry = batch.FirstOrDefault(b => b.Id == outboxId);
		if (entry is null)
			return;

		await _pushAck.MarkAcknowledgedAsync(entry, cancellationToken).ConfigureAwait(false);
	}

	private ServerSyncResult Finish(ServerSyncResult result)
	{
		LastResult = result;
		Changed?.Invoke();
		return result;
	}

	private static bool IsNetworkAvailable()
	{
#if ANDROID || IOS || MACCATALYST || WINDOWS
		return Microsoft.Maui.Networking.Connectivity.NetworkAccess == Microsoft.Maui.Networking.NetworkAccess.Internet;
#else
		return true;
#endif
	}
}
