using MRS.Application.Sync;
using Npgsql;

namespace MRS.Infrastructure.Postgres;

internal static class PostgresSyncHierarchyReader
{
	internal static async Task<IReadOnlyList<SyncOrganizationRow>> ReadOrganizationsAsync(
		NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		var list = new List<SyncOrganizationRow>();
		await using var cmd = new NpgsqlCommand(
			"SELECT id, full_name, short_name, is_active, legal_form_code FROM organizations ORDER BY id;", connection);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			list.Add(new SyncOrganizationRow(
				reader.GetInt64(0),
				reader.GetString(1),
				reader.IsDBNull(2) ? null : reader.GetString(2),
				reader.GetBoolean(3),
				reader.IsDBNull(4) ? null : reader.GetString(4)));
		}

		return list;
	}

	internal static async Task<IReadOnlyList<SyncFacilityRow>> ReadFacilitiesAsync(
		NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		var list = new List<SyncFacilityRow>();
		await using var cmd = new NpgsqlCommand("""
			SELECT f.id, f.organization_id, f.name, f.ui_flow, f.is_active, f.contract_address,
			       a.zip_code, a.city, a.street, a.building, a.structure, a.block
			FROM facilities f
			INNER JOIN organization_addresses a ON a.id = f.address_id
			ORDER BY f.id;
			""", connection);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var address = new SyncAddressPayload(
				reader.IsDBNull(6) ? null : reader.GetString(6),
				reader.GetString(7),
				reader.GetString(8),
				reader.GetString(9),
				reader.IsDBNull(10) ? null : reader.GetString(10),
				reader.IsDBNull(11) ? null : reader.GetString(11));
			list.Add(new SyncFacilityRow(
				reader.GetInt64(0),
				reader.GetInt64(1),
				reader.GetString(2),
				reader.GetString(3),
				reader.GetBoolean(4),
				reader.IsDBNull(5) ? null : reader.GetString(5),
				address));
		}

		return list;
	}

	internal static async Task<IReadOnlyList<SyncFacilitySystemRow>> ReadFacilitySystemsAsync(
		NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		var list = new List<SyncFacilitySystemRow>();
		await using var cmd = new NpgsqlCommand(
			"SELECT id, facility_id, name, description, is_active FROM facility_systems ORDER BY id;", connection);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			list.Add(new SyncFacilitySystemRow(
				reader.GetInt64(0),
				reader.GetInt64(1),
				reader.GetString(2),
				reader.IsDBNull(3) ? null : reader.GetString(3),
				reader.GetBoolean(4)));
		}

		return list;
	}

	internal static async Task<IReadOnlyList<SyncInstallationRow>> ReadInstallationsAsync(
		NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		var list = new List<SyncInstallationRow>();
		await using var cmd = new NpgsqlCommand("""
			SELECT i.id, i.system_id, i.equipment_type_id, i.is_active,
			       i.custom_name, i.custom_serial_number, i.equipment_model_id, i.custom_model_name
			FROM installations i
			ORDER BY i.id;
			""", connection);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			list.Add(new SyncInstallationRow(
				reader.GetInt64(0),
				reader.GetInt64(1),
				reader.GetInt64(2),
				reader.GetBoolean(3),
				reader.IsDBNull(4) ? null : reader.GetString(4),
				reader.IsDBNull(5) ? null : reader.GetString(5),
				reader.IsDBNull(6) ? null : reader.GetInt64(6),
				reader.IsDBNull(7) ? null : reader.GetString(7)));
		}

		return list;
	}

	internal static async Task<IReadOnlyList<SyncEquipmentTypeRow>> ReadEquipmentTypesAsync(
		NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		var list = new List<SyncEquipmentTypeRow>();
		await using var cmd = new NpgsqlCommand("SELECT id, type_name, code FROM equipment_types ORDER BY id;", connection);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			list.Add(new SyncEquipmentTypeRow(
				reader.GetInt64(0),
				reader.GetString(1),
				reader.IsDBNull(2) ? null : reader.GetString(2)));
		}

		return list;
	}

	internal static async Task<IReadOnlyList<SyncEquipmentModelRow>> ReadEquipmentModelsAsync(
		NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		var list = new List<SyncEquipmentModelRow>();
		await using var cmd = new NpgsqlCommand("""
			SELECT id, equipment_type_id, manufacturer, name
			FROM equipment_models
			WHERE equipment_type_id IS NOT NULL
			ORDER BY id;
			""", connection);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			list.Add(new SyncEquipmentModelRow(
				reader.GetInt64(0),
				reader.GetInt64(1),
				reader.IsDBNull(2) ? null : reader.GetString(2),
				reader.GetString(3)));
		}

		return list;
	}

	internal static async Task<IReadOnlyList<SyncSystemEquipmentLinkRow>> ReadSystemEquipmentLinksAsync(
		NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		var list = new List<SyncSystemEquipmentLinkRow>();
		await using var cmd = new NpgsqlCommand(
			"SELECT system_id, equipment_type_id FROM system_equipment_types ORDER BY system_id, equipment_type_id;", connection);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			list.Add(new SyncSystemEquipmentLinkRow(reader.GetInt64(0), reader.GetInt64(1)));
		return list;
	}
}
