using System.Text.Json;
using MRS.Application.Sync;
using Npgsql;

namespace MRS.Infrastructure.Postgres;

internal static class PostgresSyncHierarchyWriter
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	internal static async Task UpsertAsync(NpgsqlConnection connection, HierarchySyncPayload payload, CancellationToken cancellationToken)
	{
		await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		await UpsertOrganizationAsync(connection, tx, payload.Organization, cancellationToken).ConfigureAwait(false);
		var addressId = await UpsertFacilityAsync(connection, tx, payload.Facility, cancellationToken).ConfigureAwait(false);
		_ = addressId;
		await UpsertSystemAsync(connection, tx, payload.FacilitySystem, cancellationToken).ConfigureAwait(false);
		await UpsertEquipmentTypeAsync(connection, tx, payload.EquipmentType, cancellationToken).ConfigureAwait(false);
		await EnsureSystemEquipmentLinkAsync(connection, tx, payload.FacilitySystem.LocalId, payload.EquipmentType.LocalId, cancellationToken)
			.ConfigureAwait(false);
		await UpsertInstallationAsync(connection, tx, payload.Installation, cancellationToken).ConfigureAwait(false);

		await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
	}

	internal static HierarchySyncPayload Parse(string json) =>
		JsonSerializer.Deserialize<HierarchySyncPayload>(json, JsonOptions)
		?? throw new InvalidOperationException("Не удалось разобрать hierarchy payload.");

	private static async Task UpsertOrganizationAsync(
		NpgsqlConnection connection, NpgsqlTransaction tx, SyncOrganizationPayload org, CancellationToken cancellationToken)
	{
		await PostgresSyncIdentityGuard.EnsureNoConflictAsync(
			connection, tx, "organizations", org.LocalId, "full_name", org.FullName, cancellationToken).ConfigureAwait(false);

		await using var cmd = new NpgsqlCommand("""
			INSERT INTO organizations (id, full_name, short_name, legal_form_code, is_active)
			VALUES (@id, @full, @short, @form, @active)
			ON CONFLICT (id) DO UPDATE SET
			    full_name = EXCLUDED.full_name,
			    short_name = EXCLUDED.short_name,
			    legal_form_code = EXCLUDED.legal_form_code,
			    is_active = EXCLUDED.is_active;
			""", connection, tx);
		cmd.Parameters.AddWithValue("id", org.LocalId);
		cmd.Parameters.AddWithValue("full", org.FullName);
		cmd.Parameters.AddWithValue("short", org.ShortName ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("form", org.LegalFormCode ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("active", org.IsActive);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		await BumpSequenceAsync(connection, tx, "organizations", org.LocalId, cancellationToken).ConfigureAwait(false);
	}

	private static async Task<long> UpsertFacilityAsync(
		NpgsqlConnection connection, NpgsqlTransaction tx, SyncFacilityPayload facility, CancellationToken cancellationToken)
	{
		long addressId;
		await using (var findAddr = new NpgsqlCommand("SELECT address_id FROM facilities WHERE id = @id;", connection, tx))
		{
			findAddr.Parameters.AddWithValue("id", facility.LocalId);
			var existing = await findAddr.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (existing is long l)
				addressId = l;
			else
			{
				await using var insAddr = new NpgsqlCommand("""
					INSERT INTO organization_addresses (zip_code, country, city, street, building, structure, block)
					VALUES (@zip, 'Россия', @city, @street, @building, @structure, @block)
					RETURNING id;
					""", connection, tx);
				insAddr.Parameters.AddWithValue("zip", facility.Address.ZipCode ?? (object)DBNull.Value);
				insAddr.Parameters.AddWithValue("city", facility.Address.City);
				insAddr.Parameters.AddWithValue("street", facility.Address.Street);
				insAddr.Parameters.AddWithValue("building", facility.Address.Building);
				insAddr.Parameters.AddWithValue("structure", facility.Address.Structure ?? (object)DBNull.Value);
				insAddr.Parameters.AddWithValue("block", facility.Address.Block ?? (object)DBNull.Value);
				addressId = Convert.ToInt64(await insAddr.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
			}
		}

		await PostgresSyncIdentityGuard.EnsureNoConflictAsync(
			connection, tx, "facilities", facility.LocalId, "name", facility.Name, cancellationToken).ConfigureAwait(false);

		await using var cmd = new NpgsqlCommand("""
			INSERT INTO facilities (id, organization_id, name, address_id, contract_address, ui_flow, is_active)
			VALUES (@id, @org, @name, @addr, @contract, @flow, @active)
			ON CONFLICT (id) DO UPDATE SET
			    organization_id = EXCLUDED.organization_id,
			    name = EXCLUDED.name,
			    contract_address = EXCLUDED.contract_address,
			    ui_flow = EXCLUDED.ui_flow,
			    is_active = EXCLUDED.is_active;
			""", connection, tx);
		cmd.Parameters.AddWithValue("id", facility.LocalId);
		cmd.Parameters.AddWithValue("org", facility.OrganizationLocalId);
		cmd.Parameters.AddWithValue("name", facility.Name);
		cmd.Parameters.AddWithValue("addr", addressId);
		cmd.Parameters.AddWithValue("contract", facility.ContractAddress ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("flow", facility.UiFlow);
		cmd.Parameters.AddWithValue("active", facility.IsActive);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		await BumpSequenceAsync(connection, tx, "facilities", facility.LocalId, cancellationToken).ConfigureAwait(false);
		return addressId;
	}

	private static async Task UpsertSystemAsync(
		NpgsqlConnection connection, NpgsqlTransaction tx, SyncFacilitySystemPayload system, CancellationToken cancellationToken)
	{
		await using var cmd = new NpgsqlCommand("""
			INSERT INTO facility_systems (id, facility_id, name, description, is_active)
			VALUES (@id, @fid, @name, @descr, @active)
			ON CONFLICT (id) DO UPDATE SET
			    facility_id = EXCLUDED.facility_id,
			    name = EXCLUDED.name,
			    description = EXCLUDED.description,
			    is_active = EXCLUDED.is_active;
			""", connection, tx);
		cmd.Parameters.AddWithValue("id", system.LocalId);
		cmd.Parameters.AddWithValue("fid", system.FacilityLocalId);
		cmd.Parameters.AddWithValue("name", system.Name);
		cmd.Parameters.AddWithValue("descr", system.Description ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("active", system.IsActive);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		await BumpSequenceAsync(connection, tx, "facility_systems", system.LocalId, cancellationToken).ConfigureAwait(false);
	}

	private static async Task UpsertEquipmentTypeAsync(
		NpgsqlConnection connection, NpgsqlTransaction tx, SyncEquipmentTypePayload equipmentType, CancellationToken cancellationToken)
	{
		await PostgresSyncIdentityGuard.EnsureNoConflictAsync(
			connection, tx, "equipment_types", equipmentType.LocalId, "type_name", equipmentType.TypeName, cancellationToken)
			.ConfigureAwait(false);

		await using var cmd = new NpgsqlCommand("""
			INSERT INTO equipment_types (id, type_name, code)
			VALUES (@id, @name, @code)
			ON CONFLICT (id) DO UPDATE SET
			    type_name = EXCLUDED.type_name,
			    code = EXCLUDED.code;
			""", connection, tx);
		cmd.Parameters.AddWithValue("id", equipmentType.LocalId);
		cmd.Parameters.AddWithValue("name", equipmentType.TypeName);
		cmd.Parameters.AddWithValue("code", equipmentType.Code ?? (object)DBNull.Value);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		await BumpSequenceAsync(connection, tx, "equipment_types", equipmentType.LocalId, cancellationToken).ConfigureAwait(false);
	}

	private static async Task EnsureSystemEquipmentLinkAsync(
		NpgsqlConnection connection, NpgsqlTransaction tx, int systemId, int equipmentTypeId, CancellationToken cancellationToken)
	{
		await using var cmd = new NpgsqlCommand("""
			INSERT INTO system_equipment_types (system_id, equipment_type_id)
			VALUES (@sid, @eid)
			ON CONFLICT DO NOTHING;
			""", connection, tx);
		cmd.Parameters.AddWithValue("sid", systemId);
		cmd.Parameters.AddWithValue("eid", equipmentTypeId);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task UpsertInstallationAsync(
		NpgsqlConnection connection, NpgsqlTransaction tx, SyncInstallationPayload installation, CancellationToken cancellationToken)
	{
		long? modelId = null;
		if (!string.IsNullOrWhiteSpace(installation.Manufacturer) && !string.IsNullOrWhiteSpace(installation.ModelName))
		{
			await using var find = new NpgsqlCommand("""
				SELECT id FROM equipment_models
				WHERE equipment_type_id = @et AND TRIM(manufacturer) = @mfg AND TRIM(name) = @name
				LIMIT 1;
				""", connection, tx);
			find.Parameters.AddWithValue("et", installation.EquipmentTypeLocalId);
			find.Parameters.AddWithValue("mfg", installation.Manufacturer.Trim());
			find.Parameters.AddWithValue("name", installation.ModelName.Trim());
			var existing = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (existing is long l)
				modelId = l;
			else
			{
				await using var ins = new NpgsqlCommand("""
					INSERT INTO equipment_models (equipment_type_id, manufacturer, name)
					VALUES (@et, @mfg, @name)
					RETURNING id;
					""", connection, tx);
				ins.Parameters.AddWithValue("et", installation.EquipmentTypeLocalId);
				ins.Parameters.AddWithValue("mfg", installation.Manufacturer.Trim());
				ins.Parameters.AddWithValue("name", installation.ModelName.Trim());
				modelId = Convert.ToInt64(await ins.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
			}
		}

		await using var cmd = new NpgsqlCommand("""
			INSERT INTO installations (
			    id, system_id, equipment_type_id, equipment_model_id,
			    custom_name, custom_serial_number, custom_model_name, is_active)
			VALUES (@id, @sid, @eid, @model, @name, @serial, @modelName, @active)
			ON CONFLICT (id) DO UPDATE SET
			    system_id = EXCLUDED.system_id,
			    equipment_type_id = EXCLUDED.equipment_type_id,
			    equipment_model_id = EXCLUDED.equipment_model_id,
			    custom_name = EXCLUDED.custom_name,
			    custom_serial_number = EXCLUDED.custom_serial_number,
			    custom_model_name = EXCLUDED.custom_model_name,
			    is_active = EXCLUDED.is_active;
			""", connection, tx);
		cmd.Parameters.AddWithValue("id", installation.LocalId);
		cmd.Parameters.AddWithValue("sid", installation.SystemLocalId);
		cmd.Parameters.AddWithValue("eid", installation.EquipmentTypeLocalId);
		cmd.Parameters.AddWithValue("model", modelId ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("name", installation.CustomName ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("serial", installation.CustomSerialNumber ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("modelName", installation.ModelName ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("active", installation.IsActive);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		await BumpSequenceAsync(connection, tx, "installations", installation.LocalId, cancellationToken).ConfigureAwait(false);
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
