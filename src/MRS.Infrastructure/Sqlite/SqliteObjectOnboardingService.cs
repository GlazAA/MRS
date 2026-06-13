using Microsoft.Data.Sqlite;
using MRS.Application.Facilities;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteObjectOnboardingService : IObjectOnboardingService
{
    private readonly ILocalDatabasePath _paths;
    private readonly ILocalDatabaseBootstrapper _bootstrapper;

    public SqliteObjectOnboardingService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
    {
        _paths = paths;
        _bootstrapper = bootstrapper;
    }

    public async Task<IReadOnlyList<HierarchyOption>> GetAllEquipmentTypesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, type_name
            FROM equipment_types
            ORDER BY type_name;
            """;

        var list = new List<HierarchyOption>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(new HierarchyOption(reader.GetInt32(0), reader.GetString(1)));
        return list;
    }

    public async Task<ObjectOnboardingResult> UpsertHierarchyAsync(
        ObjectOnboardingRequest request,
        CancellationToken cancellationToken = default)
    {
        var installationLabel = (request.InstallationLabel ?? string.Empty).Trim();
        if (installationLabel.Length == 0)
            throw new InvalidOperationException("Укажите номер/название установки.");

        await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var (organizationId, organizationCreated) = await EnsureOrganizationAsync(connection, tx, request, cancellationToken).ConfigureAwait(false);
            var (facilityId, facilityCreated) = await EnsureFacilityAsync(connection, tx, request, organizationId, cancellationToken).ConfigureAwait(false);
            var (systemId, systemCreated) = await EnsureSystemAsync(connection, tx, request, facilityId, cancellationToken).ConfigureAwait(false);
            var (equipmentTypeId, equipmentTypeCreated) = await EnsureEquipmentTypeAsync(connection, tx, request, cancellationToken).ConfigureAwait(false);
            await EnsureSystemEquipmentLinkAsync(connection, tx, systemId, equipmentTypeId, cancellationToken).ConfigureAwait(false);
            var (installationId, installationCreated) = await EnsureInstallationAsync(connection, tx, request, systemId, equipmentTypeId, installationLabel, cancellationToken).ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ObjectOnboardingResult(
                organizationId,
                facilityId,
                systemId,
                equipmentTypeId,
                installationId,
                organizationCreated,
                facilityCreated,
                systemCreated,
                equipmentTypeCreated,
                installationCreated);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<(int Id, bool Created)> EnsureOrganizationAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        ObjectOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ExistingOrganizationId is int existingId)
        {
            if (!await OrganizationExistsAsync(connection, tx, existingId, cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("Выбранная организация не найдена.");
            return (existingId, false);
        }

        var full = (request.NewOrganizationFullName ?? string.Empty).Trim();
        if (full.Length == 0)
            throw new InvalidOperationException("Укажите название организации.");
        var shortName = (request.NewOrganizationShortName ?? string.Empty).Trim();

        using (var find = connection.CreateCommand())
        {
            find.Transaction = tx;
            find.CommandText = """
                SELECT id
                FROM organizations
                WHERE is_active = 1
                  AND (TRIM(full_name) = $full OR TRIM(COALESCE(short_name, '')) = $short);
                """;
            find.Parameters.AddWithValue("$full", full);
            find.Parameters.AddWithValue("$short", shortName);
            var existing = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (existing is not null)
                return (Convert.ToInt32(existing), false);
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT INTO organizations (full_name, short_name, is_active)
            VALUES ($full, $short, 1);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$full", full);
        insert.Parameters.AddWithValue("$short", shortName.Length == 0 ? DBNull.Value : shortName);
        var scalar = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return (scalar is long l ? (int)l : Convert.ToInt32(scalar), true);
    }

    private static async Task<(int Id, bool Created)> EnsureFacilityAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        ObjectOnboardingRequest request,
        int organizationId,
        CancellationToken cancellationToken)
    {
        if (request.ExistingFacilityId is int existingId)
        {
            using var check = connection.CreateCommand();
            check.Transaction = tx;
            check.CommandText = """
                SELECT COUNT(1)
                FROM facilities
                WHERE id = $id AND organization_id = $org AND is_active = 1;
                """;
            check.Parameters.AddWithValue("$id", existingId);
            check.Parameters.AddWithValue("$org", organizationId);
            var ok = Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
            if (!ok)
                throw new InvalidOperationException("Выбранный объект не найден в указанной организации.");
            return (existingId, false);
        }

        var facilityName = (request.NewFacilityName ?? string.Empty).Trim();
        if (facilityName.Length == 0)
            throw new InvalidOperationException("Укажите название объекта.");

        using (var find = connection.CreateCommand())
        {
            find.Transaction = tx;
            find.CommandText = """
                SELECT id
                FROM facilities
                WHERE organization_id = $org AND is_active = 1 AND TRIM(name) = $name
                LIMIT 1;
                """;
            find.Parameters.AddWithValue("$org", organizationId);
            find.Parameters.AddWithValue("$name", facilityName);
            var existing = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (existing is not null)
                return (Convert.ToInt32(existing), false);
        }

        var city = (request.AddressCity ?? string.Empty).Trim();
        var street = (request.AddressStreet ?? string.Empty).Trim();
        var building = (request.AddressBuilding ?? string.Empty).Trim();
        if (city.Length == 0 || street.Length == 0 || building.Length == 0)
            throw new InvalidOperationException("Для нового объекта заполните адрес: город, улица и дом.");

        int addressId;
        using (var insertAddress = connection.CreateCommand())
        {
            insertAddress.Transaction = tx;
            insertAddress.CommandText = """
                INSERT INTO organization_addresses (zip_code, country, region, city, street, building)
                VALUES ($zip, 'Россия', $region, $city, $street, $building);
                SELECT last_insert_rowid();
                """;
            insertAddress.Parameters.AddWithValue("$zip", NullIfEmpty(request.AddressZipCode));
            insertAddress.Parameters.AddWithValue("$region", NullIfEmpty(request.AddressRegion));
            insertAddress.Parameters.AddWithValue("$city", city);
            insertAddress.Parameters.AddWithValue("$street", street);
            insertAddress.Parameters.AddWithValue("$building", building);
            var s = await insertAddress.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            addressId = s is long l ? (int)l : Convert.ToInt32(s);
        }

        using var insertFacility = connection.CreateCommand();
        insertFacility.Transaction = tx;
        insertFacility.CommandText = """
            INSERT INTO facilities (organization_id, name, address_id, ui_flow, is_active)
            VALUES ($org, $name, $addr, 'hierarchical', 1);
            SELECT last_insert_rowid();
            """;
        insertFacility.Parameters.AddWithValue("$org", organizationId);
        insertFacility.Parameters.AddWithValue("$name", facilityName);
        insertFacility.Parameters.AddWithValue("$addr", addressId);
        var scalar = await insertFacility.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return (scalar is long l2 ? (int)l2 : Convert.ToInt32(scalar), true);
    }

    private static async Task<(int Id, bool Created)> EnsureSystemAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        ObjectOnboardingRequest request,
        int facilityId,
        CancellationToken cancellationToken)
    {
        if (request.ExistingSystemId is int existingId)
        {
            using var check = connection.CreateCommand();
            check.Transaction = tx;
            check.CommandText = """
                SELECT COUNT(1)
                FROM facility_systems
                WHERE id = $id AND facility_id = $fid AND is_active = 1;
                """;
            check.Parameters.AddWithValue("$id", existingId);
            check.Parameters.AddWithValue("$fid", facilityId);
            var ok = Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
            if (!ok)
                throw new InvalidOperationException("Выбранная система не принадлежит выбранному объекту.");
            return (existingId, false);
        }

        var systemName = (request.NewSystemName ?? string.Empty).Trim();
        if (systemName.Length == 0)
            throw new InvalidOperationException("Укажите название системы.");

        using (var find = connection.CreateCommand())
        {
            find.Transaction = tx;
            find.CommandText = """
                SELECT id
                FROM facility_systems
                WHERE facility_id = $fid AND is_active = 1 AND TRIM(name) = $name
                LIMIT 1;
                """;
            find.Parameters.AddWithValue("$fid", facilityId);
            find.Parameters.AddWithValue("$name", systemName);
            var existing = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (existing is not null)
                return (Convert.ToInt32(existing), false);
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT INTO facility_systems (facility_id, name, description, is_active)
            VALUES ($fid, $name, $descr, 1);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$fid", facilityId);
        insert.Parameters.AddWithValue("$name", systemName);
        insert.Parameters.AddWithValue("$descr", NullIfEmpty(request.NewSystemDescription));
        var scalar = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return (scalar is long l ? (int)l : Convert.ToInt32(scalar), true);
    }

    private static async Task<(int Id, bool Created)> EnsureEquipmentTypeAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        ObjectOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ExistingEquipmentTypeId is int existingId)
        {
            using var check = connection.CreateCommand();
            check.Transaction = tx;
            check.CommandText = "SELECT COUNT(1) FROM equipment_types WHERE id = $id;";
            check.Parameters.AddWithValue("$id", existingId);
            var ok = Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
            if (!ok)
                throw new InvalidOperationException("Выбранный тип оборудования не найден.");
            return (existingId, false);
        }

        var typeName = (request.NewEquipmentTypeName ?? string.Empty).Trim();
        if (typeName.Length == 0)
            throw new InvalidOperationException("Укажите тип оборудования.");
        var code = (request.NewEquipmentTypeCode ?? string.Empty).Trim();

        using (var find = connection.CreateCommand())
        {
            find.Transaction = tx;
            find.CommandText = """
                SELECT id
                FROM equipment_types
                WHERE TRIM(type_name) = $name
                LIMIT 1;
                """;
            find.Parameters.AddWithValue("$name", typeName);
            var existing = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (existing is not null)
                return (Convert.ToInt32(existing), false);
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT INTO equipment_types (type_name, code)
            VALUES ($name, $code);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$name", typeName);
        insert.Parameters.AddWithValue("$code", NullIfEmpty(code));
        var scalar = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return (scalar is long l ? (int)l : Convert.ToInt32(scalar), true);
    }

    private static async Task EnsureSystemEquipmentLinkAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        int systemId,
        int equipmentTypeId,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR IGNORE INTO system_equipment_types (system_id, equipment_type_id)
            VALUES ($sid, $eid);
            """;
        cmd.Parameters.AddWithValue("$sid", systemId);
        cmd.Parameters.AddWithValue("$eid", equipmentTypeId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(int Id, bool Created)> EnsureInstallationAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        ObjectOnboardingRequest request,
        int systemId,
        int equipmentTypeId,
        string installationLabel,
        CancellationToken cancellationToken)
    {
        using (var find = connection.CreateCommand())
        {
            find.Transaction = tx;
            find.CommandText = """
                SELECT id
                FROM installations
                WHERE system_id = $sid
                  AND equipment_type_id = $eid
                  AND is_active = 1
                  AND TRIM(COALESCE(custom_name, '')) = $name
                LIMIT 1;
                """;
            find.Parameters.AddWithValue("$sid", systemId);
            find.Parameters.AddWithValue("$eid", equipmentTypeId);
            find.Parameters.AddWithValue("$name", installationLabel);
            var existing = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                var id = Convert.ToInt32(existing);
                await UpdateInstallationDetailsAsync(connection, tx, id, request, cancellationToken).ConfigureAwait(false);
                return (id, false);
            }
        }

        var model = (request.InstallationModel ?? string.Empty).Trim();
        var serial = (request.InstallationSerialNumber ?? string.Empty).Trim();

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT INTO installations (
                system_id,
                equipment_type_id,
                custom_name,
                custom_model_name,
                custom_serial_number,
                is_data_modified,
                is_active
            )
            VALUES ($sid, $eid, $name, $model, $serial, $modified, 1);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$sid", systemId);
        insert.Parameters.AddWithValue("$eid", equipmentTypeId);
        insert.Parameters.AddWithValue("$name", installationLabel);
        insert.Parameters.AddWithValue("$model", NullIfEmpty(model));
        insert.Parameters.AddWithValue("$serial", NullIfEmpty(serial));
        insert.Parameters.AddWithValue("$modified", model.Length > 0 || serial.Length > 0 ? 1 : 0);
        var scalar = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return (scalar is long l ? (int)l : Convert.ToInt32(scalar), true);
    }

    private static async Task UpdateInstallationDetailsAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        int installationId,
        ObjectOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        var model = (request.InstallationModel ?? string.Empty).Trim();
        var serial = (request.InstallationSerialNumber ?? string.Empty).Trim();
        if (model.Length == 0 && serial.Length == 0)
            return;

        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE installations
            SET custom_model_name = COALESCE(NULLIF($model, ''), custom_model_name),
                custom_serial_number = COALESCE(NULLIF($serial, ''), custom_serial_number),
                is_data_modified = CASE WHEN NULLIF($model, '') IS NOT NULL OR NULLIF($serial, '') IS NOT NULL THEN 1 ELSE is_data_modified END
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$model", model);
        cmd.Parameters.AddWithValue("$serial", serial);
        cmd.Parameters.AddWithValue("$id", installationId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> OrganizationExistsAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        int organizationId,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT COUNT(1)
            FROM organizations
            WHERE id = $id AND is_active = 1;
            """;
        cmd.Parameters.AddWithValue("$id", organizationId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
    }

    private static object NullIfEmpty(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        return v.Length == 0 ? DBNull.Value : v;
    }
}
