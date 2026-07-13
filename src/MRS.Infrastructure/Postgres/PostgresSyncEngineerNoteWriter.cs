using System.Text.Json;
using MRS.Application.Sync;
using Npgsql;

namespace MRS.Infrastructure.Postgres;

internal static class PostgresSyncEngineerNoteWriter
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	internal static EngineerNoteSyncPayload Parse(string json) =>
		JsonSerializer.Deserialize<EngineerNoteSyncPayload>(json, JsonOptions)
		?? throw new InvalidOperationException("Не удалось разобрать engineer_note payload.");

	internal static async Task ApplyAsync(NpgsqlConnection connection, EngineerNoteSyncPayload payload, CancellationToken cancellationToken)
	{
		if (string.Equals(payload.Operation, "delete", StringComparison.OrdinalIgnoreCase))
		{
			await DeleteAsync(connection, payload, cancellationToken).ConfigureAwait(false);
			return;
		}

		await UpsertAsync(connection, payload, cancellationToken).ConfigureAwait(false);
	}

	private static async Task DeleteAsync(
		NpgsqlConnection connection, EngineerNoteSyncPayload payload, CancellationToken cancellationToken)
	{
		await using var cmd = new NpgsqlCommand("""
			DELETE FROM engineer_notes
			WHERE id = @id OR client_uuid = @uuid;
			""", connection);
		cmd.Parameters.AddWithValue("id", payload.LocalId);
		cmd.Parameters.AddWithValue("uuid", Guid.Parse(payload.ClientUuid));
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task UpsertAsync(
		NpgsqlConnection connection, EngineerNoteSyncPayload payload, CancellationToken cancellationToken)
	{
		Guid clientUuid = Guid.Parse(payload.ClientUuid);

		long noteId;
		await using (var find = new NpgsqlCommand(
			"SELECT id FROM engineer_notes WHERE client_uuid = @uuid OR id = @id LIMIT 1;", connection))
		{
			find.Parameters.AddWithValue("uuid", clientUuid);
			find.Parameters.AddWithValue("id", payload.LocalId);
			var existing = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			noteId = existing is long l ? l : 0;
		}

		if (noteId == 0)
		{
			await using var insert = new NpgsqlCommand("""
				INSERT INTO engineer_notes (
				    id, author_user_id, facility_id, scheduled_visit_id, checklist_id,
				    title, body, deadline_date, is_completed, completed_at,
				    client_uuid, sync_state, created_at, updated_at)
				VALUES (
				    @id, @author, @fid, @vid, @cid,
				    @title, @body, @deadline, @done, @completed,
				    @uuid, 'synced', NOW(), NOW());
				""", connection);
			BindParams(insert, payload, clientUuid);
			await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			await BumpSequenceAsync(connection, "engineer_notes", payload.LocalId, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			await using var update = new NpgsqlCommand("""
				UPDATE engineer_notes
				SET author_user_id = @author,
				    facility_id = @fid,
				    scheduled_visit_id = @vid,
				    checklist_id = @cid,
				    title = @title,
				    body = @body,
				    deadline_date = @deadline,
				    is_completed = @done,
				    completed_at = @completed,
				    client_uuid = @uuid,
				    sync_state = 'synced',
				    updated_at = NOW()
				WHERE id = @existing;
				""", connection);
			BindParams(update, payload, clientUuid);
			update.Parameters.AddWithValue("existing", noteId);
			await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private static void BindParams(NpgsqlCommand cmd, EngineerNoteSyncPayload payload, Guid clientUuid)
	{
		cmd.Parameters.AddWithValue("id", payload.LocalId);
		cmd.Parameters.AddWithValue("author", payload.AuthorUserId);
		cmd.Parameters.AddWithValue("fid", payload.FacilityId ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("vid", payload.ScheduledVisitId ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("cid", payload.ChecklistId ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("title", payload.Title ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("body", payload.Body);
		cmd.Parameters.AddWithValue("deadline", payload.DeadlineDate ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("done", payload.IsCompleted);
		cmd.Parameters.AddWithValue("completed", payload.CompletedAt ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("uuid", clientUuid);
	}

	private static async Task BumpSequenceAsync(
		NpgsqlConnection connection, string table, int id, CancellationToken cancellationToken)
	{
		await using var cmd = new NpgsqlCommand(
			$"SELECT setval(pg_get_serial_sequence('{table}', 'id'), GREATEST((SELECT COALESCE(MAX(id), 0) FROM {table}), @id));",
			connection);
		cmd.Parameters.AddWithValue("id", id);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}
}
