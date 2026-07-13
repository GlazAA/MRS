using System.Text.Json;
using MRS.Application.Sync;
using Npgsql;

namespace MRS.Infrastructure.Postgres;

public sealed class PostgresSyncService
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	private readonly PostgresConnectionFactory _factory;

	public PostgresSyncService(PostgresConnectionFactory factory)
	{
		_factory = factory;
	}

	public async Task<SyncPushResponse> PushAsync(SyncPushRequest request, int userId, CancellationToken cancellationToken = default)
	{
		var results = new List<SyncPushItemResult>();
		foreach (var item in request.Items)
		{
			try
			{
				if (string.Equals(item.EntityType, "checklist", StringComparison.OrdinalIgnoreCase))
					await UpsertChecklistAsync(item, userId, cancellationToken).ConfigureAwait(false);
				else if (string.Equals(item.EntityType, "hierarchy", StringComparison.OrdinalIgnoreCase))
					await UpsertHierarchyAsync(item, cancellationToken).ConfigureAwait(false);
				else if (string.Equals(item.EntityType, "checklist_template", StringComparison.OrdinalIgnoreCase))
					await UpsertTemplateAsync(item, cancellationToken).ConfigureAwait(false);
				else if (string.Equals(item.EntityType, "engineer_note", StringComparison.OrdinalIgnoreCase))
					await UpsertEngineerNoteAsync(item, cancellationToken).ConfigureAwait(false);
				else if (string.Equals(item.EntityType, "scheduled_visit", StringComparison.OrdinalIgnoreCase))
					await UpsertScheduledVisitAsync(item, cancellationToken).ConfigureAwait(false);
				else
					throw new InvalidOperationException($"Тип сущности не поддерживается: {item.EntityType}");

				results.Add(new SyncPushItemResult(item.OutboxId, true, null));
			}
			catch (Exception ex)
			{
				results.Add(new SyncPushItemResult(item.OutboxId, false, ex.Message));
			}
		}

		var ok = results.All(r => r.Ok);
		return new SyncPushResponse(ok, ok ? "Данные приняты сервером." : "Часть записей не принята.", results);
	}

	public async Task<SyncPullResponse> PullAsync(DateTimeOffset? since, CancellationToken cancellationToken = default)
	{
		await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);

		var organizations = await PostgresSyncHierarchyReader.ReadOrganizationsAsync(connection, cancellationToken).ConfigureAwait(false);
		var facilities = await PostgresSyncHierarchyReader.ReadFacilitiesAsync(connection, cancellationToken).ConfigureAwait(false);
		var systems = await PostgresSyncHierarchyReader.ReadFacilitySystemsAsync(connection, cancellationToken).ConfigureAwait(false);
		var installations = await PostgresSyncHierarchyReader.ReadInstallationsAsync(connection, cancellationToken).ConfigureAwait(false);
		var equipmentTypes = await PostgresSyncHierarchyReader.ReadEquipmentTypesAsync(connection, cancellationToken).ConfigureAwait(false);
		var equipmentModels = await PostgresSyncHierarchyReader.ReadEquipmentModelsAsync(connection, cancellationToken).ConfigureAwait(false);
		var systemEquipmentLinks = await PostgresSyncHierarchyReader.ReadSystemEquipmentLinksAsync(connection, cancellationToken).ConfigureAwait(false);
		var templates = await PostgresSyncTemplateReader.ReadAllAsync(connection, cancellationToken).ConfigureAwait(false);
		var engineerNotes = await PostgresSyncPullReaders.ReadEngineerNotesAsync(connection, cancellationToken).ConfigureAwait(false);
		var scheduledVisits = await PostgresSyncPullReaders.ReadScheduledVisitsAsync(connection, cancellationToken).ConfigureAwait(false);
		var checklists = await PostgresSyncChecklistReader.ReadAllAsync(connection, cancellationToken).ConfigureAwait(false);

		return new SyncPullResponse(
			DateTimeOffset.UtcNow,
			organizations,
			facilities,
			systems,
			installations,
			equipmentTypes,
			templates,
			engineerNotes,
			scheduledVisits,
			checklists,
			equipmentModels,
			systemEquipmentLinks);
	}

	private async Task UpsertHierarchyAsync(SyncPushItem item, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(item.PayloadJson))
			throw new InvalidOperationException("Пустой payload.");
		var payload = PostgresSyncHierarchyWriter.Parse(item.PayloadJson);
		await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
		await PostgresSyncHierarchyWriter.UpsertAsync(connection, payload, cancellationToken).ConfigureAwait(false);
	}

	private async Task UpsertTemplateAsync(SyncPushItem item, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(item.PayloadJson))
			throw new InvalidOperationException("Пустой payload.");
		var payload = PostgresSyncTemplateWriter.Parse(item.PayloadJson);
		await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
		await PostgresSyncTemplateWriter.UpsertAsync(connection, payload, cancellationToken).ConfigureAwait(false);
	}

	private async Task UpsertEngineerNoteAsync(SyncPushItem item, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(item.PayloadJson))
			throw new InvalidOperationException("Пустой payload.");
		var payload = PostgresSyncEngineerNoteWriter.Parse(item.PayloadJson);
		await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
		await PostgresSyncEngineerNoteWriter.ApplyAsync(connection, payload, cancellationToken).ConfigureAwait(false);
	}

	private async Task UpsertScheduledVisitAsync(SyncPushItem item, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(item.PayloadJson))
			throw new InvalidOperationException("Пустой payload.");
		var payload = PostgresSyncScheduledVisitWriter.Parse(item.PayloadJson);
		await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
		await PostgresSyncScheduledVisitWriter.UpsertAsync(connection, payload, cancellationToken).ConfigureAwait(false);
	}

	private async Task UpsertChecklistAsync(SyncPushItem item, int userId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(item.PayloadJson))
			throw new InvalidOperationException("Пустой payload.");

		var payload = JsonSerializer.Deserialize<ChecklistSyncPayload>(item.PayloadJson, JsonOptions)
			?? throw new InvalidOperationException("Не удалось разобрать payload.");

		await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
		await UpsertChecklistInternalAsync(connection, payload, userId, cancellationToken).ConfigureAwait(false);
	}

	private async Task UpsertChecklistInternalAsync(
		NpgsqlConnection connection,
		ChecklistSyncPayload payload,
		int userId,
		CancellationToken cancellationToken)
	{
		await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		Guid clientUuid = Guid.Parse(payload.ClientUuid);

		long checklistId;
		await using (var find = new NpgsqlCommand("SELECT id FROM checklists WHERE client_uuid = @uuid LIMIT 1;", connection, tx))
		{
			find.Parameters.AddWithValue("uuid", clientUuid);
			var existing = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			checklistId = existing is long l ? l : 0;
		}

		if (checklistId == 0)
		{
			await using var insert = new NpgsqlCommand("""
				INSERT INTO checklists (
				    installation_id, maintenance_type_id, checklist_template_id, engineer_id,
				    start_at, end_at, status, is_active, client_uuid, sync_state, local_updated_at)
				VALUES (@inst, @mt, @tpl, @eng, @start, @end, @status, TRUE, @uuid, 'synced', NOW())
				RETURNING id;
				""", connection, tx);
			insert.Parameters.AddWithValue("inst", payload.InstallationId);
			insert.Parameters.AddWithValue("mt", payload.MaintenanceTypeId);
			insert.Parameters.AddWithValue("tpl", payload.ChecklistTemplateId ?? (object)DBNull.Value);
			insert.Parameters.AddWithValue("eng", payload.EngineerId);
			insert.Parameters.AddWithValue("start", payload.StartAt ?? (object)DBNull.Value);
			insert.Parameters.AddWithValue("end", payload.EndAt ?? (object)DBNull.Value);
			insert.Parameters.AddWithValue("status", payload.Status);
			insert.Parameters.AddWithValue("uuid", clientUuid);
			checklistId = Convert.ToInt64(await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
		}
		else
		{
			await using var update = new NpgsqlCommand("""
				UPDATE checklists
				SET installation_id = @inst,
				    maintenance_type_id = @mt,
				    checklist_template_id = @tpl,
				    engineer_id = @eng,
				    start_at = @start,
				    end_at = @end,
				    status = @status,
				    sync_state = 'synced',
				    server_updated_at = NOW(),
				    local_updated_at = NOW()
				WHERE id = @id;
				""", connection, tx);
			update.Parameters.AddWithValue("id", checklistId);
			update.Parameters.AddWithValue("inst", payload.InstallationId);
			update.Parameters.AddWithValue("mt", payload.MaintenanceTypeId);
			update.Parameters.AddWithValue("tpl", payload.ChecklistTemplateId ?? (object)DBNull.Value);
			update.Parameters.AddWithValue("eng", payload.EngineerId);
			update.Parameters.AddWithValue("start", payload.StartAt ?? (object)DBNull.Value);
			update.Parameters.AddWithValue("end", payload.EndAt ?? (object)DBNull.Value);
			update.Parameters.AddWithValue("status", payload.Status);
			await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

			await using var del = new NpgsqlCommand("DELETE FROM checklist_responses WHERE checklist_id = @id;", connection, tx);
			del.Parameters.AddWithValue("id", checklistId);
			await del.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		foreach (var response in payload.Responses)
		{
			await using var resp = new NpgsqlCommand("""
				INSERT INTO checklist_responses (
				    checklist_id, checklist_template_item_id, text_response, numeric_response,
				    boolean_response, selected_option_id)
				VALUES (@cid, @item, @text, @num, @bool, @opt)
				RETURNING id;
				""", connection, tx);
			resp.Parameters.AddWithValue("cid", checklistId);
			resp.Parameters.AddWithValue("item", response.TemplateItemId);
			resp.Parameters.AddWithValue("text", response.TextResponse ?? (object)DBNull.Value);
			resp.Parameters.AddWithValue("num", response.NumericResponse ?? (object)DBNull.Value);
			resp.Parameters.AddWithValue("bool", response.BooleanResponse ?? (object)DBNull.Value);
			resp.Parameters.AddWithValue("opt", response.SelectedOptionId ?? (object)DBNull.Value);
			var responseId = Convert.ToInt64(await resp.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));

			if (response.MultiOptionIds is { Count: > 0 })
			{
				foreach (var optId in response.MultiOptionIds)
				{
					await using var multi = new NpgsqlCommand("""
						INSERT INTO checklist_response_multi_options (checklist_response_id, checklist_template_item_option_id)
						VALUES (@rid, @oid)
						ON CONFLICT DO NOTHING;
						""", connection, tx);
					multi.Parameters.AddWithValue("rid", responseId);
					multi.Parameters.AddWithValue("oid", optId);
					await multi.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				}
			}
		}

		await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
	}
}
