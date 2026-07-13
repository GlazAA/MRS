using System.Globalization;
using Microsoft.Data.Sqlite;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

internal static class SqliteSyncPullApplier
{
	internal static async Task ApplyAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		SyncPullResponse pull,
		CancellationToken cancellationToken)
	{
		foreach (var org in pull.Organizations)
		{
			if (await SqliteSyncMergeGuard.ShouldSkipReferenceUpsertAsync(connection, tx, "organization", org.Id, cancellationToken).ConfigureAwait(false))
				continue;

			var insertId = await SqliteSyncMergeGuard.ResolveInsertIdAsync(
				connection, tx, "organizations", org.Id, "full_name", org.FullName, cancellationToken).ConfigureAwait(false);

			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT INTO organizations (id, full_name, short_name, legal_form_code, is_active)
				VALUES ($id, $full, $short, $form, $active)
				ON CONFLICT(id) DO UPDATE SET
				    full_name = excluded.full_name,
				    short_name = excluded.short_name,
				    legal_form_code = excluded.legal_form_code,
				    is_active = excluded.is_active;
				""";
			cmd.Parameters.AddWithValue("$id", insertId);
			cmd.Parameters.AddWithValue("$full", org.FullName);
			cmd.Parameters.AddWithValue("$short", org.ShortName ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("$form", org.LegalFormCode ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("$active", org.IsActive ? 1 : 0);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		foreach (var facility in pull.Facilities)
		{
			if (await SqliteSyncMergeGuard.ShouldSkipReferenceUpsertAsync(connection, tx, "facility", facility.Id, cancellationToken).ConfigureAwait(false))
				continue;

			var insertId = await SqliteSyncMergeGuard.ResolveInsertIdAsync(
				connection, tx, "facilities", facility.Id, "name", facility.Name, cancellationToken).ConfigureAwait(false);

			var addressId = await UpsertFacilityAddressAsync(connection, tx, (int)insertId, facility.Address, cancellationToken)
				.ConfigureAwait(false);

			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT INTO facilities (id, organization_id, name, address_id, contract_address, ui_flow, is_active)
				VALUES ($id, $org, $name, $addr, $contract, $flow, $active)
				ON CONFLICT(id) DO UPDATE SET
				    organization_id = excluded.organization_id,
				    name = excluded.name,
				    address_id = excluded.address_id,
				    contract_address = excluded.contract_address,
				    ui_flow = excluded.ui_flow,
				    is_active = excluded.is_active;
				""";
			cmd.Parameters.AddWithValue("$id", insertId);
			cmd.Parameters.AddWithValue("$org", facility.OrganizationId);
			cmd.Parameters.AddWithValue("$name", facility.Name);
			cmd.Parameters.AddWithValue("$addr", addressId);
			cmd.Parameters.AddWithValue("$contract", facility.ContractAddress ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("$flow", facility.UiFlow);
			cmd.Parameters.AddWithValue("$active", facility.IsActive ? 1 : 0);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		foreach (var system in pull.FacilitySystems)
		{
			if (await SqliteSyncMergeGuard.ShouldSkipReferenceUpsertAsync(connection, tx, "facility_system", system.Id, cancellationToken).ConfigureAwait(false))
				continue;

			var insertId = await SqliteSyncMergeGuard.ResolveInsertIdAsync(
				connection, tx, "facility_systems", system.Id, "name", system.Name, cancellationToken).ConfigureAwait(false);

			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT INTO facility_systems (id, facility_id, name, description, is_active)
				VALUES ($id, $fid, $name, $desc, $active)
				ON CONFLICT(id) DO UPDATE SET
				    facility_id = excluded.facility_id,
				    name = excluded.name,
				    description = excluded.description,
				    is_active = excluded.is_active;
				""";
			cmd.Parameters.AddWithValue("$id", insertId);
			cmd.Parameters.AddWithValue("$fid", system.FacilityId);
			cmd.Parameters.AddWithValue("$name", system.Name);
			cmd.Parameters.AddWithValue("$desc", system.Description ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("$active", system.IsActive ? 1 : 0);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		foreach (var inst in pull.Installations)
		{
			if (await SqliteSyncMergeGuard.ShouldSkipReferenceUpsertAsync(connection, tx, "installation", inst.Id, cancellationToken).ConfigureAwait(false))
				continue;

			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT INTO installations (
				    id, system_id, equipment_type_id, equipment_model_id,
				    custom_name, custom_serial_number, custom_model_name, is_active)
				VALUES ($id, $sid, $eq, $model, $name, $serial, $modelName, $active)
				ON CONFLICT(id) DO UPDATE SET
				    system_id = excluded.system_id,
				    equipment_type_id = excluded.equipment_type_id,
				    equipment_model_id = excluded.equipment_model_id,
				    custom_name = excluded.custom_name,
				    custom_serial_number = excluded.custom_serial_number,
				    custom_model_name = excluded.custom_model_name,
				    is_active = excluded.is_active;
				""";
			cmd.Parameters.AddWithValue("$id", inst.Id);
			cmd.Parameters.AddWithValue("$sid", inst.SystemId);
			cmd.Parameters.AddWithValue("$eq", inst.EquipmentTypeId);
			cmd.Parameters.AddWithValue("$model", inst.EquipmentModelId ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("$name", inst.CustomName ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("$serial", inst.CustomSerialNumber ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("$modelName", inst.CustomModelName ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("$active", inst.IsActive ? 1 : 0);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		foreach (var equipmentType in pull.EquipmentTypes)
		{
			if (await SqliteSyncMergeGuard.ShouldSkipReferenceUpsertAsync(connection, tx, "equipment_type", equipmentType.Id, cancellationToken).ConfigureAwait(false))
				continue;

			var insertId = await SqliteSyncMergeGuard.ResolveInsertIdAsync(
				connection, tx, "equipment_types", equipmentType.Id, "type_name", equipmentType.TypeName, cancellationToken).ConfigureAwait(false);

			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT INTO equipment_types (id, type_name, code)
				VALUES ($id, $name, $code)
				ON CONFLICT(id) DO UPDATE SET
				    type_name = excluded.type_name,
				    code = excluded.code;
				""";
			cmd.Parameters.AddWithValue("$id", insertId);
			cmd.Parameters.AddWithValue("$name", equipmentType.TypeName);
			cmd.Parameters.AddWithValue("$code", equipmentType.Code ?? (object)DBNull.Value);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		foreach (var model in pull.EquipmentModels)
		{
			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT INTO equipment_models (id, equipment_type_id, manufacturer, name)
				VALUES ($id, $et, $mfg, $name)
				ON CONFLICT(id) DO UPDATE SET
				    equipment_type_id = excluded.equipment_type_id,
				    manufacturer = excluded.manufacturer,
				    name = excluded.name;
				""";
			cmd.Parameters.AddWithValue("$id", model.Id);
			cmd.Parameters.AddWithValue("$et", model.EquipmentTypeId);
			cmd.Parameters.AddWithValue("$mfg", model.Manufacturer ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("$name", model.Name);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		foreach (var link in pull.SystemEquipmentLinks)
		{
			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT OR IGNORE INTO system_equipment_types (system_id, equipment_type_id)
				VALUES ($sid, $eid);
				""";
			cmd.Parameters.AddWithValue("$sid", link.SystemId);
			cmd.Parameters.AddWithValue("$eid", link.EquipmentTypeId);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		foreach (var template in pull.Templates)
		{
			if (await SqliteSyncMergeGuard.ShouldSkipReferenceUpsertAsync(connection, tx, "checklist_template", template.LocalId, cancellationToken).ConfigureAwait(false))
				continue;
			await SqliteTemplateSyncApplier.UpsertAsync(connection, tx, template, cancellationToken).ConfigureAwait(false);
		}

		foreach (var note in pull.EngineerNotes)
			await ApplyEngineerNoteAsync(connection, tx, note, cancellationToken).ConfigureAwait(false);

		foreach (var visit in pull.ScheduledVisits)
			await ApplyScheduledVisitAsync(connection, tx, visit, cancellationToken).ConfigureAwait(false);

		foreach (var checklist in pull.Checklists)
			await SqliteChecklistSyncApplier.UpsertAsync(connection, tx, checklist, cancellationToken).ConfigureAwait(false);
	}

	private static async Task<int> UpsertFacilityAddressAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		int facilityId,
		SyncAddressPayload? address,
		CancellationToken cancellationToken)
	{
		if (address is null)
		{
			using var find = connection.CreateCommand();
			find.Transaction = tx;
			find.CommandText = "SELECT address_id FROM facilities WHERE id = $id;";
			find.Parameters.AddWithValue("$id", facilityId);
			var existing = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			return existing is long l ? (int)l : existing is int i ? i : 1;
		}

		int? addressId;
		using (var find = connection.CreateCommand())
		{
			find.Transaction = tx;
			find.CommandText = "SELECT address_id FROM facilities WHERE id = $id;";
			find.Parameters.AddWithValue("$id", facilityId);
			var existing = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			addressId = existing is long l ? (int)l : existing is int i ? i : null;
		}

		if (addressId is int aid)
		{
			using var upd = connection.CreateCommand();
			upd.Transaction = tx;
			upd.CommandText = """
				UPDATE organization_addresses
				SET zip_code = $zip, city = $city, street = $street, building = $building,
				    structure = $structure, block = $block
				WHERE id = $id;
				""";
			upd.Parameters.AddWithValue("$id", aid);
			upd.Parameters.AddWithValue("$zip", address.ZipCode ?? (object)DBNull.Value);
			upd.Parameters.AddWithValue("$city", address.City);
			upd.Parameters.AddWithValue("$street", address.Street);
			upd.Parameters.AddWithValue("$building", address.Building);
			upd.Parameters.AddWithValue("$structure", address.Structure ?? (object)DBNull.Value);
			upd.Parameters.AddWithValue("$block", address.Block ?? (object)DBNull.Value);
			await upd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			return aid;
		}

		using var ins = connection.CreateCommand();
		ins.Transaction = tx;
		ins.CommandText = """
			INSERT INTO organization_addresses (zip_code, country, city, street, building, structure, block)
			VALUES ($zip, 'Россия', $city, $street, $building, $structure, $block);
			SELECT last_insert_rowid();
			""";
		ins.Parameters.AddWithValue("$zip", address.ZipCode ?? (object)DBNull.Value);
		ins.Parameters.AddWithValue("$city", address.City);
		ins.Parameters.AddWithValue("$street", address.Street);
		ins.Parameters.AddWithValue("$building", address.Building);
		ins.Parameters.AddWithValue("$structure", address.Structure ?? (object)DBNull.Value);
		ins.Parameters.AddWithValue("$block", address.Block ?? (object)DBNull.Value);
		var scalar = await ins.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is long nl ? (int)nl : Convert.ToInt32(scalar ?? 1);
	}

	private static async Task ApplyEngineerNoteAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		SyncEngineerNotePullRow note,
		CancellationToken cancellationToken)
	{
		if (await SqliteSyncMergeGuard.ShouldSkipUuidEntityAsync(
			connection, tx, "engineer_notes", "engineer_note", note.ClientUuid, note.Id, note.UpdatedAt, cancellationToken).ConfigureAwait(false))
			return;

		var targetId = await ResolveUuidRowIdAsync(connection, tx, "engineer_notes", note.ClientUuid, note.Id, cancellationToken)
			.ConfigureAwait(false);

		using var cmd = connection.CreateCommand();
		cmd.Transaction = tx;
		cmd.CommandText = """
			INSERT INTO engineer_notes (
			    id, author_user_id, facility_id, scheduled_visit_id, checklist_id,
			    title, body, deadline_date, is_completed, completed_at,
			    client_uuid, sync_state, created_at, updated_at)
			VALUES (
			    $id, $author, $fid, $vid, $cid,
			    $title, $body, $deadline, $done, $completed,
			    $uuid, 'synced', datetime('now'), $updated)
			ON CONFLICT(id) DO UPDATE SET
			    author_user_id = excluded.author_user_id,
			    facility_id = excluded.facility_id,
			    scheduled_visit_id = excluded.scheduled_visit_id,
			    checklist_id = excluded.checklist_id,
			    title = excluded.title,
			    body = excluded.body,
			    deadline_date = excluded.deadline_date,
			    is_completed = excluded.is_completed,
			    completed_at = excluded.completed_at,
			    client_uuid = excluded.client_uuid,
			    sync_state = 'synced',
			    updated_at = excluded.updated_at;
			""";
		cmd.Parameters.AddWithValue("$id", targetId);
		cmd.Parameters.AddWithValue("$author", note.AuthorUserId);
		cmd.Parameters.AddWithValue("$fid", note.FacilityId ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$vid", note.ScheduledVisitId ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$cid", note.ChecklistId ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$title", note.Title ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$body", note.Body);
		cmd.Parameters.AddWithValue("$deadline", note.DeadlineDate?.ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$done", note.IsCompleted ? 1 : 0);
		cmd.Parameters.AddWithValue("$completed", note.CompletedAt.HasValue ? note.CompletedAt.Value.ToString("O") : DBNull.Value);
		cmd.Parameters.AddWithValue("$uuid", note.ClientUuid);
		cmd.Parameters.AddWithValue("$updated", note.UpdatedAt.ToString("O"));
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task ApplyScheduledVisitAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		SyncScheduledVisitPullRow visit,
		CancellationToken cancellationToken)
	{
		if (await SqliteSyncMergeGuard.ShouldSkipUuidEntityAsync(
			connection, tx, "scheduled_visits", "scheduled_visit", visit.ClientUuid, visit.Id, visit.UpdatedAt, cancellationToken).ConfigureAwait(false))
			return;

		var targetId = await ResolveUuidRowIdAsync(connection, tx, "scheduled_visits", visit.ClientUuid, visit.Id, cancellationToken)
			.ConfigureAwait(false);

		using var cmd = connection.CreateCommand();
		cmd.Transaction = tx;
		cmd.CommandText = """
			INSERT INTO scheduled_visits (
			    id, facility_id, assigned_user_id, planned_start, planned_end, notes,
			    status, contact_employee_id, contact_manual_text, prep_skipped,
			    client_uuid, sync_state, updated_at)
			VALUES (
			    $id, $fid, $uid, $start, $end, $notes,
			    $status, $contactId, $contactManual, $prepSkipped,
			    $uuid, 'synced', $updated)
			ON CONFLICT(id) DO UPDATE SET
			    facility_id = excluded.facility_id,
			    assigned_user_id = excluded.assigned_user_id,
			    planned_start = excluded.planned_start,
			    planned_end = excluded.planned_end,
			    notes = excluded.notes,
			    status = excluded.status,
			    contact_employee_id = excluded.contact_employee_id,
			    contact_manual_text = excluded.contact_manual_text,
			    prep_skipped = excluded.prep_skipped,
			    client_uuid = excluded.client_uuid,
			    sync_state = 'synced',
			    updated_at = excluded.updated_at;
			""";
		var primaryEngineer = visit.EngineerUserIds.FirstOrDefault();
		cmd.Parameters.AddWithValue("$id", targetId);
		cmd.Parameters.AddWithValue("$fid", visit.FacilityId);
		cmd.Parameters.AddWithValue("$uid", primaryEngineer == 0 ? DBNull.Value : primaryEngineer);
		cmd.Parameters.AddWithValue("$start", visit.PlannedStart.ToString("O", CultureInfo.InvariantCulture));
		cmd.Parameters.AddWithValue("$end", visit.PlannedEnd?.ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$notes", visit.Notes ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$status", visit.Status);
		cmd.Parameters.AddWithValue("$contactId", visit.ContactEmployeeId ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$contactManual", visit.ContactManualText ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("$prepSkipped", visit.PrepSkipped ? 1 : 0);
		cmd.Parameters.AddWithValue("$uuid", visit.ClientUuid);
		cmd.Parameters.AddWithValue("$updated", visit.UpdatedAt.ToString("O"));
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

		await ReplaceVisitEngineersAsync(connection, tx, (int)targetId, visit.EngineerUserIds, cancellationToken).ConfigureAwait(false);
	}

	private static async Task<long> ResolveUuidRowIdAsync(
		SqliteConnection connection,
		SqliteTransaction tx,
		string tableName,
		string clientUuid,
		long serverId,
		CancellationToken cancellationToken)
	{
		using (var byUuid = connection.CreateCommand())
		{
			byUuid.Transaction = tx;
			byUuid.CommandText = $"""
				SELECT id FROM {tableName}
				WHERE client_uuid = $uuid
				LIMIT 1;
				""";
			byUuid.Parameters.AddWithValue("$uuid", clientUuid);
			var match = await byUuid.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (match is long l)
				return l;
			if (match is int i)
				return i;
		}

		using var byId = connection.CreateCommand();
		byId.Transaction = tx;
		byId.CommandText = $"""
			SELECT client_uuid FROM {tableName}
			WHERE id = $id
			LIMIT 1;
			""";
		byId.Parameters.AddWithValue("$id", serverId);
		var existingUuid = await byId.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		if (existingUuid is null or DBNull)
			return serverId;

		var existing = Convert.ToString(existingUuid);
		if (string.Equals(existing, clientUuid, StringComparison.OrdinalIgnoreCase))
			return serverId;

		using var next = connection.CreateCommand();
		next.Transaction = tx;
		next.CommandText = $"SELECT COALESCE(MAX(id), 0) + 1 FROM {tableName};";
		var scalar = await next.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is long nl ? nl : Convert.ToInt64(scalar ?? serverId);
	}

	private static async Task ReplaceVisitEngineersAsync(
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

		foreach (var userId in engineerUserIds.Distinct())
		{
			using var ins = connection.CreateCommand();
			ins.Transaction = tx;
			ins.CommandText = """
				INSERT OR IGNORE INTO scheduled_visit_engineers (scheduled_visit_id, user_id)
				VALUES ($vid, $uid);
				""";
			ins.Parameters.AddWithValue("$vid", visitId);
			ins.Parameters.AddWithValue("$uid", userId);
			await ins.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
	}
}
