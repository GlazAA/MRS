using System.Globalization;
using Microsoft.Data.Sqlite;
using MRS.Application;
using MRS.Application.Facilities;
using MRS.Application.Storage;
using MRS.Application.Sync;
using MRS.Application.Visits;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteScheduledVisitService : IScheduledVisitService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;
	private readonly ISyncOutboxService _outbox;

	public SqliteScheduledVisitService(
		ILocalDatabasePath paths,
		ILocalDatabaseBootstrapper bootstrapper,
		ISyncOutboxService outbox)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
		_outbox = outbox;
	}

	public async Task<IReadOnlyList<ScheduledVisitCalendarItem>> GetCalendarMonthAsync(int year, int month, CancellationToken cancellationToken = default)
	{
		var monthStart = new DateOnly(year, month, 1);
		var monthEnd = monthStart.AddMonths(1).AddDays(-1);

		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT
				sv.id,
				f.id AS facility_id,
				o.full_name,
				o.short_name,
				o.legal_form_code,
				f.name,
				a.city,
				a.street,
				a.building,
				a.structure,
				a.block,
				sv.planned_start,
				sv.planned_end,
				sv.prep_skipped,
				EXISTS (
					SELECT 1 FROM engineer_notes n
					WHERE n.scheduled_visit_id = sv.id AND n.is_completed = 0
				) AS has_open_prep,
				EXISTS (
					SELECT 1 FROM engineer_notes n
					WHERE n.scheduled_visit_id = sv.id
				) AS has_any_prep
			FROM scheduled_visits sv
			INNER JOIN facilities f ON f.id = sv.facility_id
			INNER JOIN organizations o ON o.id = f.organization_id
			INNER JOIN organization_addresses a ON a.id = f.address_id
			WHERE sv.planned_start IS NOT NULL
			  AND date(sv.planned_start) <= date($monthEnd)
			  AND date(COALESCE(sv.planned_end, sv.planned_start)) >= date($monthStart)
			ORDER BY sv.planned_start, sv.id;
			""";
		cmd.Parameters.AddWithValue("$monthStart", monthStart.ToString("O", CultureInfo.InvariantCulture));
		cmd.Parameters.AddWithValue("$monthEnd", monthEnd.ToString("O", CultureInfo.InvariantCulture));

		var items = new List<ScheduledVisitCalendarItem>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var visitId = reader.GetInt32(0);
			var facilityId = reader.GetInt32(1);
			var org = SqliteOrganizationName.ReadListName(reader, 2, 3, 4);
			var facilityName = reader.GetString(5);
			var address = FacilityAddressFormatter.Format(
				reader.GetString(6), reader.GetString(7), reader.GetString(8),
				reader.IsDBNull(9) ? null : reader.GetString(9),
				reader.IsDBNull(10) ? null : reader.GetString(10));
			var facilityLabel = $"{facilityName} ({address})";

			var start = ParseDateOnly(reader, 11) ?? monthStart;
			var end = ParseDateOnly(reader, 12) ?? start;
			var prepSkipped = !reader.IsDBNull(13) && reader.GetInt32(13) == 1;
			var hasOpenPrep = reader.GetInt32(14) == 1;
			var hasAnyPrep = reader.GetInt32(15) == 1;
			var tone = ComputeTone(end, prepSkipped, hasOpenPrep, hasAnyPrep);

			var from = start < monthStart ? monthStart : start;
			var to = end > monthEnd ? monthEnd : end;
			for (var day = from; day <= to; day = day.AddDays(1))
			{
				items.Add(new ScheduledVisitCalendarItem(
					visitId, facilityId, org, facilityLabel, day, start, end == start ? null : end, tone, hasOpenPrep));
			}
		}

		return items;
	}

	public async Task<ScheduledVisitDetail?> GetDetailAsync(int visitId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);

		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT
				sv.id,
				o.id,
				o.full_name,
				o.short_name,
				o.legal_form_code,
				f.id,
				f.name,
				a.city,
				a.street,
				a.building,
				a.structure,
				a.block,
				sv.contact_employee_id,
				e.first_name,
				e.last_name,
				COALESCE(e.middle_name, ''),
				COALESCE(e.position, ''),
				sv.contact_manual_text,
				sv.planned_start,
				sv.planned_end,
				sv.notes,
				sv.prep_skipped,
				EXISTS (
					SELECT 1 FROM engineer_notes n
					WHERE n.scheduled_visit_id = sv.id AND n.is_completed = 0
				) AS has_open_prep,
				EXISTS (
					SELECT 1 FROM engineer_notes n
					WHERE n.scheduled_visit_id = sv.id
				) AS has_any_prep
			FROM scheduled_visits sv
			INNER JOIN facilities f ON f.id = sv.facility_id
			INNER JOIN organizations o ON o.id = f.organization_id
			INNER JOIN organization_addresses a ON a.id = f.address_id
			LEFT JOIN organization_employees e ON e.id = sv.contact_employee_id
			WHERE sv.id = $id;
			""";
		cmd.Parameters.AddWithValue("$id", visitId);

		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			return null;

		var orgId = reader.GetInt32(1);
		var org = SqliteOrganizationName.ReadListName(reader, 2, 3, 4);
		var facilityId = reader.GetInt32(5);
		var facilityName = reader.GetString(6);
		var address = FacilityAddressFormatter.Format(
			reader.GetString(7), reader.GetString(8), reader.GetString(9),
			reader.IsDBNull(10) ? null : reader.GetString(10),
			reader.IsDBNull(11) ? null : reader.GetString(11));
		var facilityLabel = $"{facilityName} ({address})";

		int? contactId = reader.IsDBNull(12) ? null : reader.GetInt32(12);
		string? contactLabel = null;
		if (contactId is not null)
		{
			var ln = reader.GetString(13);
			var fn = reader.GetString(14);
			var mn = reader.GetString(15);
			var pos = reader.GetString(16);
			var name = string.IsNullOrWhiteSpace(mn) ? $"{ln} {fn}" : $"{ln} {fn} {mn}";
			contactLabel = string.IsNullOrWhiteSpace(pos) ? name.Trim() : $"{name.Trim()} — {pos}";
		}

		var contactManual = reader.IsDBNull(17) ? null : reader.GetString(17);
		var start = ParseDateOnly(reader, 18) ?? DateOnly.FromDateTime(DateTime.Today);
		var end = ParseDateOnly(reader, 19);
		var notes = reader.IsDBNull(20) ? null : reader.GetString(20);
		var prepSkipped = !reader.IsDBNull(21) && reader.GetInt32(21) == 1;
		var hasOpenPrep = reader.GetInt32(22) == 1;
		var hasAnyPrep = reader.GetInt32(23) == 1;
		var tone = ComputeTone(end ?? start, prepSkipped, hasOpenPrep, hasAnyPrep);

		var engineers = await LoadEngineersAsync(connection, visitId, cancellationToken).ConfigureAwait(false);
		var prepNotes = await LoadPrepNotesAsync(connection, visitId, cancellationToken).ConfigureAwait(false);

		return new ScheduledVisitDetail(
			visitId, orgId, org, facilityId, facilityLabel,
			contactId, contactLabel, contactManual,
			start, end == start ? null : end,
			notes, prepSkipped,
			engineers.Ids, engineers.Labels, tone, prepNotes);
	}

	public async Task<int> CreateAsync(CreateScheduledVisitRequest request, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			var clientUuid = Guid.NewGuid().ToString();
			var primaryEngineer = request.EngineerUserIds.FirstOrDefault();
			using var ins = connection.CreateCommand();
			ins.Transaction = tx;
			ins.CommandText = """
				INSERT INTO scheduled_visits (
					facility_id, assigned_user_id, planned_start, planned_end, notes,
					contact_employee_id, contact_manual_text, prep_skipped, status,
					client_uuid, sync_state, updated_at)
				VALUES (
					$fid, $uid, $start, $end, $notes,
					$contactId, $contactManual, 0, 'planned',
					$uuid, 'pending_upload', datetime('now'));
				SELECT last_insert_rowid();
				""";
			ins.Parameters.AddWithValue("$fid", request.FacilityId);
			ins.Parameters.AddWithValue("$uid", primaryEngineer == 0 ? DBNull.Value : primaryEngineer);
			ins.Parameters.AddWithValue("$uuid", clientUuid);
			ins.Parameters.AddWithValue("$start", request.PlannedStart.ToString("O", CultureInfo.InvariantCulture));
			ins.Parameters.AddWithValue("$end", request.PlannedEnd.HasValue
				? request.PlannedEnd.Value.ToString("O", CultureInfo.InvariantCulture)
				: DBNull.Value);
			ins.Parameters.AddWithValue("$notes", string.IsNullOrWhiteSpace(request.Notes) ? DBNull.Value : request.Notes.Trim());
			ins.Parameters.AddWithValue("$contactId", request.ContactEmployeeId.HasValue ? request.ContactEmployeeId.Value : DBNull.Value);
			ins.Parameters.AddWithValue("$contactManual", string.IsNullOrWhiteSpace(request.ContactManualText) ? DBNull.Value : request.ContactManualText.Trim());

			var scalar = await ins.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			var visitId = scalar is long l ? (int)l : Convert.ToInt32(scalar ?? throw new InvalidOperationException("Не удалось создать выезд."));

			await ReplaceEngineersAsync(connection, tx, visitId, request.EngineerUserIds, cancellationToken).ConfigureAwait(false);
			await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
			await EnqueueVisitSyncAsync(visitId, "insert", cancellationToken).ConfigureAwait(false);
			return visitId;
		}
		catch
		{
			await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
			throw;
		}
	}

	public async Task UpdateDatesAsync(UpdateScheduledVisitDatesRequest request, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			UPDATE scheduled_visits
			SET planned_start = $start,
			    planned_end = $end,
			    updated_at = datetime('now'),
			    sync_state = 'pending_upload'
			WHERE id = $id;
			""";
		cmd.Parameters.AddWithValue("$id", request.VisitId);
		cmd.Parameters.AddWithValue("$start", request.PlannedStart.ToString("O", CultureInfo.InvariantCulture));
		cmd.Parameters.AddWithValue("$end", request.PlannedEnd.HasValue
			? request.PlannedEnd.Value.ToString("O", CultureInfo.InvariantCulture)
			: DBNull.Value);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		await EnqueueVisitSyncAsync(request.VisitId, "update", cancellationToken).ConfigureAwait(false);
	}

	public async Task SetPrepSkippedAsync(int visitId, bool skipped, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			UPDATE scheduled_visits
			SET prep_skipped = $skip,
			    updated_at = datetime('now'),
			    sync_state = 'pending_upload'
			WHERE id = $id;
			""";
		cmd.Parameters.AddWithValue("$id", visitId);
		cmd.Parameters.AddWithValue("$skip", skipped ? 1 : 0);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		await EnqueueVisitSyncAsync(visitId, "update", cancellationToken).ConfigureAwait(false);
	}

	private async Task EnqueueVisitSyncAsync(int visitId, string operation, CancellationToken cancellationToken)
	{
		var json = await SqliteScheduledVisitSyncPayloadBuilder.BuildAsync(_paths, _bootstrapper, visitId, operation, cancellationToken)
			.ConfigureAwait(false);
		var payload = System.Text.Json.JsonSerializer.Deserialize<ScheduledVisitSyncPayload>(json);
		var uuid = payload?.ClientUuid ?? $"visit-{visitId}";
		await _outbox.EnqueueAsync(new SyncOutboxEnqueueRequest("scheduled_visit", uuid, operation, json), cancellationToken)
			.ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<VisitFilterOption>> ListForFilterAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT sv.id, o.full_name, o.short_name, o.legal_form_code, f.name, sv.planned_start, sv.planned_end
			FROM scheduled_visits sv
			INNER JOIN facilities f ON f.id = sv.facility_id
			INNER JOIN organizations o ON o.id = f.organization_id
			ORDER BY sv.planned_start DESC, sv.id DESC;
			""";

		var list = new List<VisitFilterOption>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var id = reader.GetInt32(0);
			var org = SqliteOrganizationName.ReadListName(reader, 1, 2, 3);
			var facility = reader.GetString(4);
			var start = ParseDateOnly(reader, 5) ?? DateOnly.FromDateTime(DateTime.Today);
			var end = ParseDateOnly(reader, 6);
			var label = $"{MrsDateFormat.FormatDate(start)} — {org}, {facility}";
			list.Add(new VisitFilterOption(id, label, start, end == start ? null : end));
		}

		return list;
	}

	private static VisitCalendarTone ComputeTone(DateOnly lastDay, bool prepSkipped, bool hasOpenPrepNote, bool hasAnyPrepNote)
	{
		if (lastDay < DateOnly.FromDateTime(DateTime.Today))
			return VisitCalendarTone.Past;
		if (prepSkipped)
			return VisitCalendarTone.Ready;
		if (hasOpenPrepNote || !hasAnyPrepNote)
			return VisitCalendarTone.PrepPending;
		return VisitCalendarTone.Ready;
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

	private static async Task<(IReadOnlyList<int> Ids, IReadOnlyList<string> Labels)> LoadEngineersAsync(
		SqliteConnection connection, int visitId, CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT u.id, u.first_name, u.last_name, COALESCE(u.middle_name, '')
			FROM scheduled_visit_engineers sve
			INNER JOIN users u ON u.id = sve.user_id
			WHERE sve.scheduled_visit_id = $id
			ORDER BY u.last_name, u.first_name;
			""";
		cmd.Parameters.AddWithValue("$id", visitId);

		var ids = new List<int>();
		var labels = new List<string>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			ids.Add(reader.GetInt32(0));
			var first = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
			var last = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
			var middle = reader.GetString(3);
			labels.Add(string.IsNullOrWhiteSpace(middle) ? $"{last} {first}".Trim() : $"{last} {first} {middle}".Trim());
		}

		return (ids, labels);
	}

	private static async Task<IReadOnlyList<LinkedPrepNoteSummary>> LoadPrepNotesAsync(
		SqliteConnection connection, int visitId, CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT id, body, deadline_date, is_completed
			FROM engineer_notes
			WHERE scheduled_visit_id = $id
			ORDER BY created_at;
			""";
		cmd.Parameters.AddWithValue("$id", visitId);

		var list = new List<LinkedPrepNoteSummary>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var id = reader.GetInt32(0);
			var body = reader.GetString(1);
			DateOnly? deadline = null;
			if (!reader.IsDBNull(2))
			{
				var raw = reader.GetString(2);
				if (DateOnly.TryParse(raw, out var d))
					deadline = d;
			}

			list.Add(new LinkedPrepNoteSummary(id, body, deadline, reader.GetInt32(3) == 1));
		}

		return list;
	}

	private static async Task ReplaceEngineersAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		int visitId,
		IReadOnlyList<int> engineerUserIds,
		CancellationToken cancellationToken)
	{
		using (var del = connection.CreateCommand())
		{
			del.Transaction = tx;
			del.CommandText = "DELETE FROM scheduled_visit_engineers WHERE scheduled_visit_id = $id;";
			del.Parameters.AddWithValue("$id", visitId);
			await del.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		foreach (var uid in engineerUserIds.Distinct())
		{
			using var ins = connection.CreateCommand();
			ins.Transaction = tx;
			ins.CommandText = """
				INSERT OR IGNORE INTO scheduled_visit_engineers (scheduled_visit_id, user_id)
				VALUES ($vid, $uid);
				""";
			ins.Parameters.AddWithValue("$vid", visitId);
			ins.Parameters.AddWithValue("$uid", uid);
			await ins.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
	}
}
