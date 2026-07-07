using System.Text.Json;
using Microsoft.Data.Sqlite;
using MRS.Application.Facilities;
using MRS.Application.Storage;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

internal static class SqliteHierarchySyncPayloadBuilder
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	internal static async Task<string> BuildAsync(
		ILocalDatabasePath paths,
		ILocalDatabaseBootstrapper bootstrapper,
		ObjectOnboardingResult result,
		CancellationToken cancellationToken)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(paths, bootstrapper, cancellationToken)
			.ConfigureAwait(false);

		SyncOrganizationPayload org = await LoadOrganizationAsync(connection, result.OrganizationId, cancellationToken).ConfigureAwait(false);
		SyncFacilityPayload facility = await LoadFacilityAsync(connection, result.FacilityId, cancellationToken).ConfigureAwait(false);
		SyncFacilitySystemPayload system = await LoadSystemAsync(connection, result.SystemId, cancellationToken).ConfigureAwait(false);
		SyncEquipmentTypePayload equipmentType = await LoadEquipmentTypeAsync(connection, result.EquipmentTypeId, cancellationToken).ConfigureAwait(false);
		SyncInstallationPayload installation = await LoadInstallationAsync(connection, result.InstallationId, cancellationToken).ConfigureAwait(false);

		var payload = new HierarchySyncPayload(
			Guid.NewGuid().ToString(),
			org,
			facility,
			system,
			equipmentType,
			installation);

		return JsonSerializer.Serialize(payload, JsonOptions);
	}

	private static async Task<SyncOrganizationPayload> LoadOrganizationAsync(
		SqliteConnection connection, int id, CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT full_name, short_name, legal_form_code, is_active FROM organizations WHERE id = $id;";
		cmd.Parameters.AddWithValue("$id", id);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			throw new InvalidOperationException($"Организация {id} не найдена.");
		return new SyncOrganizationPayload(
			id,
			reader.GetString(0),
			reader.IsDBNull(1) ? null : reader.GetString(1),
			reader.IsDBNull(2) ? null : reader.GetString(2),
			reader.GetInt32(3) != 0);
	}

	private static async Task<SyncFacilityPayload> LoadFacilityAsync(
		SqliteConnection connection, int id, CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT f.organization_id, f.name, f.contract_address, f.ui_flow, f.is_active,
			       a.zip_code, a.city, a.street, a.building, a.structure, a.block
			FROM facilities f
			INNER JOIN organization_addresses a ON a.id = f.address_id
			WHERE f.id = $id;
			""";
		cmd.Parameters.AddWithValue("$id", id);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			throw new InvalidOperationException($"Объект {id} не найден.");
		var address = new SyncAddressPayload(
			reader.IsDBNull(5) ? null : reader.GetString(5),
			reader.GetString(6),
			reader.GetString(7),
			reader.GetString(8),
			reader.IsDBNull(9) ? null : reader.GetString(9),
			reader.IsDBNull(10) ? null : reader.GetString(10));
		return new SyncFacilityPayload(
			id,
			reader.GetInt32(0),
			reader.GetString(1),
			reader.IsDBNull(2) ? null : reader.GetString(2),
			reader.GetString(3),
			reader.GetInt32(4) != 0,
			address);
	}

	private static async Task<SyncFacilitySystemPayload> LoadSystemAsync(
		SqliteConnection connection, int id, CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT facility_id, name, description, is_active FROM facility_systems WHERE id = $id;";
		cmd.Parameters.AddWithValue("$id", id);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			throw new InvalidOperationException($"Система {id} не найдена.");
		return new SyncFacilitySystemPayload(
			id,
			reader.GetInt32(0),
			reader.GetString(1),
			reader.IsDBNull(2) ? null : reader.GetString(2),
			reader.GetInt32(3) != 0);
	}

	private static async Task<SyncEquipmentTypePayload> LoadEquipmentTypeAsync(
		SqliteConnection connection, int id, CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT type_name, code FROM equipment_types WHERE id = $id;";
		cmd.Parameters.AddWithValue("$id", id);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			throw new InvalidOperationException($"Тип оборудования {id} не найден.");
		return new SyncEquipmentTypePayload(
			id,
			reader.GetString(0),
			reader.IsDBNull(1) ? null : reader.GetString(1));
	}

	private static async Task<SyncInstallationPayload> LoadInstallationAsync(
		SqliteConnection connection, int id, CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT i.system_id, i.equipment_type_id, i.custom_name, i.custom_serial_number, i.is_active,
			       em.manufacturer, COALESCE(em.name, i.custom_model_name)
			FROM installations i
			LEFT JOIN equipment_models em ON em.id = i.equipment_model_id
			WHERE i.id = $id;
			""";
		cmd.Parameters.AddWithValue("$id", id);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			throw new InvalidOperationException($"Установка {id} не найдена.");
		return new SyncInstallationPayload(
			id,
			reader.GetInt32(0),
			reader.GetInt32(1),
			reader.IsDBNull(2) ? null : reader.GetString(2),
			reader.IsDBNull(3) ? null : reader.GetString(3),
			reader.IsDBNull(5) ? null : reader.GetString(5),
			reader.IsDBNull(6) ? null : reader.GetString(6),
			reader.GetInt32(4) != 0);
	}
}
