using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

internal static class SqliteScheduledVisitSyncPayloadBuilder
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	internal static async Task<string> BuildAsync(
		ILocalDatabasePath paths,
		ILocalDatabaseBootstrapper bootstrapper,
		int visitId,
		string operation,
		CancellationToken cancellationToken)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(paths, bootstrapper, cancellationToken)
			.ConfigureAwait(false);

		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT facility_id, contact_employee_id, contact_manual_text,
			       planned_start, planned_end, notes, prep_skipped, status, client_uuid
			FROM scheduled_visits
			WHERE id = $id;
			""";
		cmd.Parameters.AddWithValue("$id", visitId);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			throw new InvalidOperationException($"Выезд {visitId} не найден.");

		var facilityId = reader.GetInt32(0);
		int? contactEmployeeId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
		var contactManual = reader.IsDBNull(2) ? null : reader.GetString(2);
		var start = ParseDateOnly(reader, 3) ?? DateOnly.FromDateTime(DateTime.Today);
		var end = ParseDateOnly(reader, 4);
		var notes = reader.IsDBNull(5) ? null : reader.GetString(5);
		var prepSkipped = !reader.IsDBNull(6) && reader.GetInt32(6) != 0;
		var status = reader.IsDBNull(7) ? "planned" : reader.GetString(7);
		var clientUuid = reader.IsDBNull(8) ? $"visit-{visitId}" : reader.GetString(8);
		await reader.CloseAsync().ConfigureAwait(false);

		var engineers = await LoadEngineersAsync(connection, visitId, cancellationToken).ConfigureAwait(false);

		var payload = new ScheduledVisitSyncPayload(
			clientUuid,
			visitId,
			facilityId,
			contactEmployeeId,
			contactManual,
			start,
			end == start ? null : end,
			notes,
			prepSkipped,
			status,
			engineers,
			operation);

		return JsonSerializer.Serialize(payload, JsonOptions);
	}

	private static async Task<IReadOnlyList<int>> LoadEngineersAsync(
		SqliteConnection connection,
		int visitId,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT user_id FROM scheduled_visit_engineers WHERE scheduled_visit_id = $id ORDER BY user_id;";
		cmd.Parameters.AddWithValue("$id", visitId);
		var list = new List<int>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			list.Add(reader.GetInt32(0));
		return list;
	}

	private static DateOnly? ParseDateOnly(SqliteDataReader reader, int ordinal)
	{
		if (reader.IsDBNull(ordinal))
			return null;
		var raw = reader.GetString(ordinal);
		if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
			return d;
		if (SqliteDateTimeParsing.TryParseStored(raw, out var dto))
			return DateOnly.FromDateTime(dto.LocalDateTime);
		return null;
	}
}
