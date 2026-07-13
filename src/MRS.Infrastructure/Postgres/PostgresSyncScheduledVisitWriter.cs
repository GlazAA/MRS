using System.Text.Json;
using MRS.Application.Sync;
using Npgsql;

namespace MRS.Infrastructure.Postgres;

internal static class PostgresSyncScheduledVisitWriter
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	internal static ScheduledVisitSyncPayload Parse(string json) =>
		JsonSerializer.Deserialize<ScheduledVisitSyncPayload>(json, JsonOptions)
		?? throw new InvalidOperationException("Не удалось разобрать scheduled_visit payload.");

	internal static async Task UpsertAsync(
		NpgsqlConnection connection, ScheduledVisitSyncPayload payload, CancellationToken cancellationToken)
	{
		await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		Guid clientUuid = Guid.TryParse(payload.ClientUuid, out var parsed) ? parsed : Guid.NewGuid();
		var primaryEngineer = payload.EngineerUserIds.FirstOrDefault();
		await using (var cmd = new NpgsqlCommand("""
			INSERT INTO scheduled_visits (
			    id, facility_id, assigned_user_id, planned_start, planned_end, notes,
			    status, contact_employee_id, contact_manual_text, prep_skipped, client_uuid, updated_at)
			VALUES (
			    @id, @fid, @uid, @start, @end, @notes,
			    @status, @contactId, @contactManual, @prepSkipped, @uuid, NOW())
			ON CONFLICT (id) DO UPDATE SET
			    facility_id = EXCLUDED.facility_id,
			    assigned_user_id = EXCLUDED.assigned_user_id,
			    planned_start = EXCLUDED.planned_start,
			    planned_end = EXCLUDED.planned_end,
			    notes = EXCLUDED.notes,
			    status = EXCLUDED.status,
			    contact_employee_id = EXCLUDED.contact_employee_id,
			    contact_manual_text = EXCLUDED.contact_manual_text,
			    prep_skipped = EXCLUDED.prep_skipped,
			    client_uuid = COALESCE(scheduled_visits.client_uuid, EXCLUDED.client_uuid),
			    updated_at = NOW();
			""", connection, tx))
		{
			cmd.Parameters.AddWithValue("id", payload.LocalId);
			cmd.Parameters.AddWithValue("fid", payload.FacilityId);
			cmd.Parameters.AddWithValue("uid", primaryEngineer == 0 ? DBNull.Value : primaryEngineer);
			cmd.Parameters.AddWithValue("start", payload.PlannedStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
			cmd.Parameters.AddWithValue("end", payload.PlannedEnd.HasValue
				? payload.PlannedEnd.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
				: DBNull.Value);
			cmd.Parameters.AddWithValue("notes", payload.Notes ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("status", payload.Status);
			cmd.Parameters.AddWithValue("contactId", payload.ContactEmployeeId ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("contactManual", payload.ContactManualText ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("prepSkipped", payload.PrepSkipped);
			cmd.Parameters.AddWithValue("uuid", clientUuid);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		await BumpSequenceAsync(connection, tx, "scheduled_visits", payload.LocalId, cancellationToken).ConfigureAwait(false);
		await ReplaceEngineersAsync(connection, tx, payload.LocalId, payload.EngineerUserIds, cancellationToken).ConfigureAwait(false);

		await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task ReplaceEngineersAsync(
		NpgsqlConnection connection,
		NpgsqlTransaction tx,
		int visitId,
		IReadOnlyList<int> engineerUserIds,
		CancellationToken cancellationToken)
	{
		await using (var del = new NpgsqlCommand("DELETE FROM scheduled_visit_engineers WHERE scheduled_visit_id = @id;", connection, tx))
		{
			del.Parameters.AddWithValue("id", visitId);
			await del.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		foreach (var userId in engineerUserIds.Distinct())
		{
			await using var ins = new NpgsqlCommand("""
				INSERT INTO scheduled_visit_engineers (scheduled_visit_id, user_id)
				VALUES (@vid, @uid)
				ON CONFLICT DO NOTHING;
				""", connection, tx);
			ins.Parameters.AddWithValue("vid", visitId);
			ins.Parameters.AddWithValue("uid", userId);
			await ins.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private static async Task BumpSequenceAsync(
		NpgsqlConnection connection, NpgsqlTransaction tx, string table, int id, CancellationToken cancellationToken)
	{
		await using var cmd = new NpgsqlCommand(
			$"SELECT setval(pg_get_serial_sequence('{table}', 'id'), GREATEST((SELECT COALESCE(MAX(id), 0) FROM {table}), @id));",
			connection, tx);
		cmd.Parameters.AddWithValue("id", id);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}
}
