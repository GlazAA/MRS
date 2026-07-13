using MRS.Application.Sync;
using Npgsql;

namespace MRS.Infrastructure.Postgres;

internal static class PostgresSyncPullReaders
{
	internal static async Task<IReadOnlyList<SyncEngineerNotePullRow>> ReadEngineerNotesAsync(
		NpgsqlConnection connection,
		CancellationToken cancellationToken)
	{
		var list = new List<SyncEngineerNotePullRow>();
		await using var cmd = new NpgsqlCommand("""
			SELECT id, author_user_id, body, deadline_date, title, facility_id,
			       scheduled_visit_id, checklist_id, is_completed, completed_at,
			       client_uuid, updated_at
			FROM engineer_notes
			WHERE client_uuid IS NOT NULL
			ORDER BY id;
			""", connection);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			list.Add(new SyncEngineerNotePullRow(
				reader.GetGuid(10).ToString(),
				reader.GetInt64(0),
				reader.GetInt32(1),
				reader.GetString(2),
				reader.IsDBNull(3) ? null : DateOnly.FromDateTime(reader.GetDateTime(3)),
				reader.IsDBNull(4) ? null : reader.GetString(4),
				reader.IsDBNull(5) ? null : reader.GetInt32(5),
				reader.IsDBNull(6) ? null : reader.GetInt32(6),
				reader.IsDBNull(7) ? null : reader.GetInt32(7),
				reader.GetBoolean(8),
				reader.IsDBNull(9) ? null : reader.GetDateTime(9),
				reader.GetDateTime(11)));
		}

		return list;
	}

	internal static async Task<IReadOnlyList<SyncScheduledVisitPullRow>> ReadScheduledVisitsAsync(
		NpgsqlConnection connection,
		CancellationToken cancellationToken)
	{
		var ids = new List<int>();
		await using (var cmd = new NpgsqlCommand("SELECT id FROM scheduled_visits ORDER BY id;", connection))
		{
			await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				ids.Add(reader.GetInt32(0));
		}

		var list = new List<SyncScheduledVisitPullRow>(ids.Count);
		foreach (var id in ids)
			list.Add(await ReadScheduledVisitAsync(connection, id, cancellationToken).ConfigureAwait(false));
		return list;
	}

	private static async Task<SyncScheduledVisitPullRow> ReadScheduledVisitAsync(
		NpgsqlConnection connection,
		int visitId,
		CancellationToken cancellationToken)
	{
		long id;
		int facilityId;
		int? contactEmployeeId;
		string? contactManualText;
		DateOnly plannedStart;
		DateOnly? plannedEnd;
		string? notes;
		bool prepSkipped;
		string status;
		string clientUuid;
		DateTimeOffset updatedAt;

		await using (var cmd = new NpgsqlCommand("""
			SELECT id, facility_id, contact_employee_id, contact_manual_text,
			       planned_start, planned_end, notes, prep_skipped, status,
			       client_uuid, updated_at
			FROM scheduled_visits
			WHERE id = @id;
			""", connection))
		{
			cmd.Parameters.AddWithValue("id", visitId);
			await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				throw new InvalidOperationException($"Выезд {visitId} не найден.");

			id = reader.GetInt64(0);
			facilityId = reader.GetInt32(1);
			contactEmployeeId = reader.IsDBNull(2) ? null : reader.GetInt32(2);
			contactManualText = reader.IsDBNull(3) ? null : reader.GetString(3);
			plannedStart = DateOnly.FromDateTime(reader.GetDateTime(4));
			plannedEnd = reader.IsDBNull(5) ? null : DateOnly.FromDateTime(reader.GetDateTime(5));
			notes = reader.IsDBNull(6) ? null : reader.GetString(6);
			prepSkipped = reader.GetBoolean(7);
			status = reader.GetString(8);
			clientUuid = reader.IsDBNull(9) ? $"visit-{visitId}" : reader.GetGuid(9).ToString();
			updatedAt = reader.GetDateTime(10);
		}

		var engineers = await LoadEngineersAsync(connection, visitId, cancellationToken).ConfigureAwait(false);
		return new SyncScheduledVisitPullRow(
			clientUuid, id, facilityId, contactEmployeeId, contactManualText,
			plannedStart, plannedEnd, notes, prepSkipped, status, engineers, updatedAt);
	}

	private static async Task<IReadOnlyList<int>> LoadEngineersAsync(
		NpgsqlConnection connection,
		int visitId,
		CancellationToken cancellationToken)
	{
		var list = new List<int>();
		await using var cmd = new NpgsqlCommand(
			"SELECT user_id FROM scheduled_visit_engineers WHERE scheduled_visit_id = @id ORDER BY user_id;", connection);
		cmd.Parameters.AddWithValue("id", visitId);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			list.Add(reader.GetInt32(0));
		return list;
	}
}
