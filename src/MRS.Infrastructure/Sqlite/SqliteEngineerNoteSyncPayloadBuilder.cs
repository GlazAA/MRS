using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

internal static class SqliteEngineerNoteSyncPayloadBuilder
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	internal static async Task<string> BuildAsync(
		ILocalDatabasePath paths,
		ILocalDatabaseBootstrapper bootstrapper,
		int noteId,
		string operation,
		CancellationToken cancellationToken)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(paths, bootstrapper, cancellationToken)
			.ConfigureAwait(false);

		var clientUuid = await EnsureClientUuidAsync(connection, noteId, cancellationToken).ConfigureAwait(false);

		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT author_user_id, body, deadline_date, title, facility_id,
			       scheduled_visit_id, checklist_id, is_completed, completed_at
			FROM engineer_notes
			WHERE id = $id;
			""";
		cmd.Parameters.AddWithValue("$id", noteId);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			throw new InvalidOperationException($"Заметка {noteId} не найдена.");

		DateOnly? deadline = null;
		if (!reader.IsDBNull(2))
		{
			var raw = reader.GetString(2);
			if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
				deadline = d;
		}

		DateTimeOffset? completedAt = null;
		if (!reader.IsDBNull(8))
		{
			var raw = reader.GetString(8);
			if (SqliteDateTimeParsing.TryParseStored(raw, out var dt))
				completedAt = dt;
		}

		var payload = new EngineerNoteSyncPayload(
			clientUuid,
			noteId,
			reader.GetInt32(0),
			reader.GetString(1),
			deadline,
			reader.IsDBNull(3) ? null : reader.GetString(3),
			reader.IsDBNull(4) ? null : reader.GetInt32(4),
			reader.IsDBNull(5) ? null : reader.GetInt32(5),
			reader.IsDBNull(6) ? null : reader.GetInt32(6),
			reader.GetInt32(7) != 0,
			completedAt,
			operation);

		return JsonSerializer.Serialize(payload, JsonOptions);
	}

	internal static async Task<string> BuildDeleteAsync(
		ILocalDatabasePath paths,
		ILocalDatabaseBootstrapper bootstrapper,
		int noteId,
		string clientUuid,
		CancellationToken cancellationToken)
	{
		var payload = new EngineerNoteSyncPayload(
			clientUuid,
			noteId,
			0,
			string.Empty,
			null,
			null,
			null,
			null,
			null,
			false,
			null,
			"delete");
		return JsonSerializer.Serialize(payload, JsonOptions);
	}

	private static async Task<string> EnsureClientUuidAsync(
		SqliteConnection connection,
		int noteId,
		CancellationToken cancellationToken)
	{
		using var read = connection.CreateCommand();
		read.CommandText = "SELECT client_uuid FROM engineer_notes WHERE id = $id;";
		read.Parameters.AddWithValue("$id", noteId);
		var existing = await read.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		if (existing is string s && !string.IsNullOrWhiteSpace(s))
			return s;

		var uuid = Guid.NewGuid().ToString();
		using var upd = connection.CreateCommand();
		upd.CommandText = "UPDATE engineer_notes SET client_uuid = $uuid WHERE id = $id;";
		upd.Parameters.AddWithValue("$uuid", uuid);
		upd.Parameters.AddWithValue("$id", noteId);
		await upd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		return uuid;
	}
}
