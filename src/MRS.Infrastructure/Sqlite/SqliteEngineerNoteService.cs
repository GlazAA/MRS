using System.Globalization;
using Microsoft.Data.Sqlite;
using MRS.Application.Facilities;
using MRS.Application.Notes;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteEngineerNoteService : IEngineerNoteService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteEngineerNoteService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<IReadOnlyList<EngineerNoteListItem>> ListAsync(EngineerNoteFilter filter, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT
				n.id,
				n.title,
				n.body,
				n.deadline_date,
				n.is_completed,
				n.completed_at,
				n.updated_at,
				n.facility_id,
				f.name,
				a.city,
				a.street,
				a.building,
				a.structure,
				a.block,
				n.scheduled_visit_id,
				sv.planned_start,
				n.checklist_id,
				c.start_at,
				mt.type_name,
				n.author_user_id,
				u.first_name,
				u.last_name
			FROM engineer_notes n
			LEFT JOIN facilities f ON f.id = n.facility_id
			LEFT JOIN organization_addresses a ON a.id = f.address_id
			LEFT JOIN scheduled_visits sv ON sv.id = n.scheduled_visit_id
			LEFT JOIN checklists c ON c.id = n.checklist_id
			LEFT JOIN maintenance_types mt ON mt.id = c.maintenance_type_id
			LEFT JOIN users u ON u.id = n.author_user_id
			WHERE 1=1
			""";

		if (filter.FacilityId is int fid)
		{
			cmd.CommandText += " AND n.facility_id = $fid";
			cmd.Parameters.AddWithValue("$fid", fid);
		}

		if (filter.ScheduledVisitId is int vid)
		{
			cmd.CommandText += " AND n.scheduled_visit_id = $vid";
			cmd.Parameters.AddWithValue("$vid", vid);
		}

		if (filter.ChecklistId is int cid)
		{
			cmd.CommandText += " AND n.checklist_id = $cid";
			cmd.Parameters.AddWithValue("$cid", cid);
		}

		if (filter.DeadlineOnDay is DateOnly day)
		{
			cmd.CommandText += " AND date(n.deadline_date) = date($deadlineDay)";
			cmd.Parameters.AddWithValue("$deadlineDay", day.ToString("O", CultureInfo.InvariantCulture));
		}
		else if (filter.DeadlineOnOrBefore is DateOnly before)
		{
			cmd.CommandText += " AND date(n.deadline_date) <= date($deadlineBefore)";
			cmd.Parameters.AddWithValue("$deadlineBefore", before.ToString("O", CultureInfo.InvariantCulture));
		}

		cmd.CommandText += """
			 ORDER BY n.is_completed ASC,
			          CASE WHEN n.deadline_date IS NULL THEN 1 ELSE 0 END,
			          n.deadline_date ASC,
			          n.updated_at DESC;
			""";

		var list = new List<EngineerNoteListItem>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			list.Add(ReadListItem(reader));

		return list;
	}

	public async Task<EngineerNoteDetail?> GetDetailAsync(int noteId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);

		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT
				n.id, n.title, n.body, n.deadline_date, n.is_completed, n.completed_at,
				n.created_at, n.updated_at,
				n.facility_id, f.name, a.city, a.street, a.building, a.structure, a.block,
				n.scheduled_visit_id, sv.planned_start,
				n.checklist_id, c.start_at, mt.type_name,
				n.author_user_id, u.first_name, u.last_name
			FROM engineer_notes n
			LEFT JOIN facilities f ON f.id = n.facility_id
			LEFT JOIN organization_addresses a ON a.id = f.address_id
			LEFT JOIN scheduled_visits sv ON sv.id = n.scheduled_visit_id
			LEFT JOIN checklists c ON c.id = n.checklist_id
			LEFT JOIN maintenance_types mt ON mt.id = c.maintenance_type_id
			LEFT JOIN users u ON u.id = n.author_user_id
			WHERE n.id = $id;
			""";
		cmd.Parameters.AddWithValue("$id", noteId);

		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			return null;

		var readId = reader.GetInt32(0);
		var title = reader.IsDBNull(1) ? null : reader.GetString(1);
		var body = reader.GetString(2);
		DateOnly? deadline = null;
		if (!reader.IsDBNull(3) && DateOnly.TryParse(reader.GetString(3), out var dd))
			deadline = dd;
		var isCompleted = reader.GetInt32(4) == 1;
		var completedAt = ParseDateTimeOffset(reader, 5);
		var createdAt = ParseDateTimeOffset(reader, 6) ?? DateTimeOffset.Now;
		var updatedAt = ParseDateTimeOffset(reader, 7) ?? DateTimeOffset.Now;

		int? facilityId = reader.IsDBNull(8) ? null : reader.GetInt32(8);
		string? facilityLabel = null;
		if (facilityId is not null && !reader.IsDBNull(9))
		{
			var name = reader.GetString(9);
			var address = FacilityAddressFormatter.Format(
				reader.GetString(10), reader.GetString(11), reader.GetString(12),
				reader.IsDBNull(13) ? null : reader.GetString(13),
				reader.IsDBNull(14) ? null : reader.GetString(14));
			facilityLabel = $"{name} ({address})";
		}

		int? visitId = reader.IsDBNull(15) ? null : reader.GetInt32(15);
		string? visitLabel = null;
		if (visitId is not null && !reader.IsDBNull(16))
		{
			var start = ParseDateOnlyString(reader.GetString(16));
			visitLabel = start.HasValue ? $"Выезд {start:dd.MM.yyyy}" : $"Выезд #{visitId}";
		}

		int? checklistId = reader.IsDBNull(17) ? null : reader.GetInt32(17);
		string? checklistLabel = null;
		if (checklistId is not null)
		{
			var mt = reader.IsDBNull(19) ? "КЛ" : reader.GetString(19);
			checklistLabel = $"КЛ №{checklistId} • {mt}";
		}

		var authorId = reader.GetInt32(20);
		var fn = reader.IsDBNull(21) ? string.Empty : reader.GetString(21);
		var ln = reader.IsDBNull(22) ? string.Empty : reader.GetString(22);
		var author = $"{ln} {fn}".Trim();
		if (author.Length == 0)
			author = $"Пользователь #{authorId}";

		var revisions = await LoadRevisionsAsync(connection, readId, cancellationToken).ConfigureAwait(false);

		return new EngineerNoteDetail(
			readId, title, body, deadline, isCompleted, completedAt,
			createdAt, updatedAt,
			facilityId, facilityLabel, visitId, visitLabel, checklistId, checklistLabel,
			authorId, author, revisions);
	}

	public async Task<int> CreateAsync(CreateEngineerNoteRequest request, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			INSERT INTO engineer_notes (
				author_user_id, facility_id, scheduled_visit_id, checklist_id,
				title, body, deadline_date, is_completed, sync_state, created_at, updated_at)
			VALUES (
				$author, $fid, $vid, $cid,
				$title, $body, $deadline, 0, 'local', datetime('now'), datetime('now'));
			SELECT last_insert_rowid();
			""";
		BindNoteParams(cmd, request.AuthorUserId, request.Body, request.DeadlineDate, request.Title,
			request.FacilityId, request.ScheduledVisitId, request.ChecklistId);

		var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is long l ? (int)l : Convert.ToInt32(scalar ?? throw new InvalidOperationException("Не удалось создать заметку."));
	}

	public async Task UpdateAsync(UpdateEngineerNoteRequest request, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			string oldBody;
			string? oldDeadline;
			using (var read = connection.CreateCommand())
			{
				read.Transaction = tx;
				read.CommandText = "SELECT body, deadline_date FROM engineer_notes WHERE id = $id;";
				read.Parameters.AddWithValue("$id", request.NoteId);
				await using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
				if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
					throw new InvalidOperationException("Заметка не найдена.");
				oldBody = reader.GetString(0);
				oldDeadline = reader.IsDBNull(1) ? null : reader.GetString(1);
			}

			var newBody = request.Body.Trim();
			var newDeadline = request.DeadlineDate?.ToString("O", CultureInfo.InvariantCulture);
			if (!string.Equals(oldBody, newBody, StringComparison.Ordinal) ||
			    !string.Equals(oldDeadline, newDeadline, StringComparison.Ordinal))
			{
				using var rev = connection.CreateCommand();
				rev.Transaction = tx;
				rev.CommandText = """
					INSERT INTO engineer_note_revisions (engineer_note_id, body, deadline_date, edited_by_user_id, edited_at)
					VALUES ($nid, $body, $deadline, $editor, datetime('now'));
					""";
				rev.Parameters.AddWithValue("$nid", request.NoteId);
				rev.Parameters.AddWithValue("$body", oldBody);
				rev.Parameters.AddWithValue("$deadline", oldDeadline is null ? DBNull.Value : oldDeadline);
				rev.Parameters.AddWithValue("$editor", request.EditorUserId);
				await rev.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}

			using var upd = connection.CreateCommand();
			upd.Transaction = tx;
			upd.CommandText = """
				UPDATE engineer_notes
				SET title = $title,
				    body = $body,
				    deadline_date = $deadline,
				    facility_id = $fid,
				    scheduled_visit_id = $vid,
				    checklist_id = $cid,
				    updated_at = datetime('now')
				WHERE id = $id;
				""";
			upd.Parameters.AddWithValue("$id", request.NoteId);
			upd.Parameters.AddWithValue("$title", string.IsNullOrWhiteSpace(request.Title) ? DBNull.Value : request.Title.Trim());
			upd.Parameters.AddWithValue("$body", newBody);
			upd.Parameters.AddWithValue("$deadline", newDeadline is null ? DBNull.Value : newDeadline);
			upd.Parameters.AddWithValue("$fid", request.FacilityId.HasValue ? request.FacilityId.Value : DBNull.Value);
			upd.Parameters.AddWithValue("$vid", request.ScheduledVisitId.HasValue ? request.ScheduledVisitId.Value : DBNull.Value);
			upd.Parameters.AddWithValue("$cid", request.ChecklistId.HasValue ? request.ChecklistId.Value : DBNull.Value);
			await upd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

			await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
			throw;
		}
	}

	public async Task DeleteAsync(int noteId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "DELETE FROM engineer_notes WHERE id = $id;";
		cmd.Parameters.AddWithValue("$id", noteId);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task SetCompletedAsync(int noteId, bool completed, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			UPDATE engineer_notes
			SET is_completed = $done,
			    completed_at = CASE WHEN $done = 1 THEN datetime('now') ELSE NULL END,
			    updated_at = datetime('now')
			WHERE id = $id;
			""";
		cmd.Parameters.AddWithValue("$id", noteId);
		cmd.Parameters.AddWithValue("$done", completed ? 1 : 0);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static void BindNoteParams(
		SqliteCommand cmd,
		int authorUserId,
		string body,
		DateOnly? deadline,
		string? title,
		int? facilityId,
		int? visitId,
		int? checklistId)
	{
		cmd.Parameters.AddWithValue("$author", authorUserId);
		cmd.Parameters.AddWithValue("$body", body.Trim());
		cmd.Parameters.AddWithValue("$deadline", deadline.HasValue
			? deadline.Value.ToString("O", CultureInfo.InvariantCulture)
			: DBNull.Value);
		cmd.Parameters.AddWithValue("$title", string.IsNullOrWhiteSpace(title) ? DBNull.Value : title.Trim());
		cmd.Parameters.AddWithValue("$fid", facilityId.HasValue ? facilityId.Value : DBNull.Value);
		cmd.Parameters.AddWithValue("$vid", visitId.HasValue ? visitId.Value : DBNull.Value);
		cmd.Parameters.AddWithValue("$cid", checklistId.HasValue ? checklistId.Value : DBNull.Value);
	}

	private static EngineerNoteListItem ReadListItem(SqliteDataReader reader)
	{
		var noteId = reader.GetInt32(0);
		var title = reader.IsDBNull(1) ? null : reader.GetString(1);
		var body = reader.GetString(2);
		var preview = body.Length <= 120 ? body : body[..117] + "…";

		DateOnly? deadline = null;
		if (!reader.IsDBNull(3))
		{
			var raw = reader.GetString(3);
			if (DateOnly.TryParse(raw, out var d))
				deadline = d;
		}

		var isCompleted = reader.GetInt32(4) == 1;
		var completedAt = ParseDateTimeOffset(reader, 5);
		var updatedAt = ParseDateTimeOffset(reader, 6) ?? DateTimeOffset.Now;

		int? facilityId = reader.IsDBNull(7) ? null : reader.GetInt32(7);
		string? facilityLabel = null;
		if (facilityId is not null && !reader.IsDBNull(8))
		{
			var name = reader.GetString(8);
			var address = FacilityAddressFormatter.Format(
				reader.GetString(9), reader.GetString(10), reader.GetString(11),
				reader.IsDBNull(12) ? null : reader.GetString(12),
				reader.IsDBNull(13) ? null : reader.GetString(13));
			facilityLabel = $"{name} ({address})";
		}

		int? visitId = reader.IsDBNull(14) ? null : reader.GetInt32(14);
		string? visitLabel = null;
		if (visitId is not null && !reader.IsDBNull(15))
		{
			var start = ParseDateOnlyString(reader.GetString(15));
			visitLabel = start.HasValue ? $"Выезд {start:dd.MM.yyyy}" : $"Выезд #{visitId}";
		}

		int? checklistId = reader.IsDBNull(16) ? null : reader.GetInt32(16);
		string? checklistLabel = null;
		if (checklistId is not null)
		{
			var mt = reader.IsDBNull(18) ? "КЛ" : reader.GetString(18);
			checklistLabel = $"КЛ №{checklistId} • {mt}";
		}

		var authorId = reader.GetInt32(19);
		var fn = reader.IsDBNull(20) ? string.Empty : reader.GetString(20);
		var ln = reader.IsDBNull(21) ? string.Empty : reader.GetString(21);
		var author = $"{ln} {fn}".Trim();
		if (author.Length == 0)
			author = $"Пользователь #{authorId}";

		return new EngineerNoteListItem(
			noteId, title, preview, deadline, isCompleted, completedAt, updatedAt,
			facilityId, facilityLabel, visitId, visitLabel, checklistId, checklistLabel,
			authorId, author);
	}

	private static async Task<IReadOnlyList<EngineerNoteRevision>> LoadRevisionsAsync(
		SqliteConnection connection, int noteId, CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT r.id, r.body, r.deadline_date, r.edited_by_user_id, u.first_name, u.last_name, r.edited_at
			FROM engineer_note_revisions r
			INNER JOIN users u ON u.id = r.edited_by_user_id
			WHERE r.engineer_note_id = $id
			ORDER BY r.edited_at DESC;
			""";
		cmd.Parameters.AddWithValue("$id", noteId);

		var list = new List<EngineerNoteRevision>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			DateOnly? deadline = null;
			if (!reader.IsDBNull(2))
			{
				var raw = reader.GetString(2);
				if (DateOnly.TryParse(raw, out var d))
					deadline = d;
			}

			var editorId = reader.GetInt32(3);
			var fn = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
			var ln = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
			var editor = $"{ln} {fn}".Trim();
			var editedAt = ParseDateTimeOffset(reader, 6) ?? DateTimeOffset.Now;

			list.Add(new EngineerNoteRevision(reader.GetInt32(0), reader.GetString(1), deadline, editorId, editor, editedAt));
		}

		return list;
	}

	private static DateOnly? ParseDateOnlyString(string raw) =>
		DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

	private static DateTimeOffset? ParseDateTimeOffset(SqliteDataReader reader, int ordinal)
	{
		if (reader.IsDBNull(ordinal))
			return null;
		var raw = reader.GetString(ordinal);
		if (SqliteDateTimeParsing.TryParseStored(raw, out var dto))
			return dto;
		if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
			return parsed;
		return null;
	}
}
