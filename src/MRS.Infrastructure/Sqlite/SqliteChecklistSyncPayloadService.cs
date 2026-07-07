using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteChecklistSyncPayloadService : IChecklistSyncPayloadService
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteChecklistSyncPayloadService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task EnsureClientUuidAsync(int checklistId, CancellationToken cancellationToken = default)
	{
		var existing = await GetClientUuidAsync(checklistId, cancellationToken).ConfigureAwait(false);
		if (!string.IsNullOrWhiteSpace(existing))
			return;

		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			UPDATE checklists
			SET client_uuid = $uuid
			WHERE id = $id AND (client_uuid IS NULL OR client_uuid = '');
			""";
		cmd.Parameters.AddWithValue("$id", checklistId);
		cmd.Parameters.AddWithValue("$uuid", Guid.NewGuid().ToString());
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task<string?> GetClientUuidAsync(int checklistId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT client_uuid FROM checklists WHERE id = $id;";
		cmd.Parameters.AddWithValue("$id", checklistId);
		var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is string s && s.Length > 0 ? s : null;
	}

	public async Task<string> BuildChecklistJsonAsync(int checklistId, string clientUuid, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);

		using var headerCmd = connection.CreateCommand();
		headerCmd.CommandText = """
			SELECT installation_id, maintenance_type_id, checklist_template_id, engineer_id,
			       start_at, end_at, status
			FROM checklists
			WHERE id = $id AND is_active = 1;
			""";
		headerCmd.Parameters.AddWithValue("$id", checklistId);
		await using var headerReader = await headerCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await headerReader.ReadAsync(cancellationToken).ConfigureAwait(false))
			throw new InvalidOperationException($"Контрольный лист {checklistId} не найден.");

		var installationId = headerReader.GetInt32(0);
		var maintenanceTypeId = headerReader.GetInt32(1);
		int? templateId = headerReader.IsDBNull(2) ? null : headerReader.GetInt32(2);
		var engineerId = headerReader.GetInt32(3);
		DateTimeOffset? startAt = ParseOptionalDate(headerReader, 4);
		DateTimeOffset? endAt = ParseOptionalDate(headerReader, 5);
		var status = headerReader.GetString(6);
		await headerReader.CloseAsync().ConfigureAwait(false);

		var responses = await LoadResponsesAsync(connection, checklistId, cancellationToken).ConfigureAwait(false);
		var payload = new ChecklistSyncPayload(
			clientUuid,
			checklistId,
			installationId,
			maintenanceTypeId,
			templateId,
			engineerId,
			startAt,
			endAt,
			status,
			responses);

		return JsonSerializer.Serialize(payload, JsonOptions);
	}

	public async Task MarkSyncedAsync(int checklistId, CancellationToken cancellationToken = default)
	{
		await SqliteChecklistSyncHelper.MarkChecklistSyncedAsync(_paths, _bootstrapper, checklistId, cancellationToken)
			.ConfigureAwait(false);
	}

	private static DateTimeOffset? ParseOptionalDate(SqliteDataReader reader, int ordinal)
	{
		if (reader.IsDBNull(ordinal))
			return null;
		return SqliteDateTimeParsing.TryParseStored(reader.GetString(ordinal), out var dt) ? dt : null;
	}

	private static async Task<IReadOnlyList<ChecklistResponseSyncPayload>> LoadResponsesAsync(
		SqliteConnection connection,
		int checklistId,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT id, checklist_template_item_id, text_response, numeric_response, boolean_response, selected_option_id
			FROM checklist_responses
			WHERE checklist_id = $id;
			""";
		cmd.Parameters.AddWithValue("$id", checklistId);

		var list = new List<ChecklistResponseSyncPayload>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var responseId = reader.GetInt32(0);
			var templateItemId = reader.GetInt32(1);
			var text = reader.IsDBNull(2) ? null : reader.GetString(2);
			double? numeric = reader.IsDBNull(3) ? null : reader.GetDouble(3);
			bool? boolean = reader.IsDBNull(4) ? null : reader.GetInt32(4) != 0;
			int? selected = reader.IsDBNull(5) ? null : reader.GetInt32(5);
			var multi = await LoadMultiOptionsAsync(connection, responseId, cancellationToken).ConfigureAwait(false);

			list.Add(new ChecklistResponseSyncPayload(
				templateItemId,
				text,
				numeric,
				boolean,
				selected,
				multi.Count > 0 ? multi : null));
		}

		return list;
	}

	private static async Task<List<int>> LoadMultiOptionsAsync(
		SqliteConnection connection,
		int responseId,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT checklist_template_item_option_id
			FROM checklist_response_multi_options
			WHERE checklist_response_id = $rid;
			""";
		cmd.Parameters.AddWithValue("$rid", responseId);
		var list = new List<int>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			list.Add(reader.GetInt32(0));
		return list;
	}
}

internal static class SqliteChecklistSyncHelper
{
	public static async Task EnqueueChecklistAsync(
		ISyncOutboxService outbox,
		IChecklistSyncPayloadService payloadService,
		int checklistId,
		string operation,
		CancellationToken cancellationToken)
	{
		await payloadService.EnsureClientUuidAsync(checklistId, cancellationToken).ConfigureAwait(false);
		var clientUuid = await payloadService.GetClientUuidAsync(checklistId, cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(clientUuid))
			return;

		var json = await payloadService.BuildChecklistJsonAsync(checklistId, clientUuid, cancellationToken).ConfigureAwait(false);
		await outbox.EnqueueAsync(new SyncOutboxEnqueueRequest("checklist", clientUuid, operation, json), cancellationToken)
			.ConfigureAwait(false);
	}

	public static async Task MarkChecklistSyncedAsync(
		ILocalDatabasePath paths,
		ILocalDatabaseBootstrapper bootstrapper,
		int checklistId,
		CancellationToken cancellationToken)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(paths, bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			UPDATE checklists
			SET sync_state = 'synced', server_updated_at = datetime('now')
			WHERE id = $id;
			""";
		cmd.Parameters.AddWithValue("$id", checklistId);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}
}
