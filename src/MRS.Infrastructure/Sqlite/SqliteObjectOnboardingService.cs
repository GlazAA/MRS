using Microsoft.Data.Sqlite;
using MRS.Application.Facilities;
using MRS.Application.Storage;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteObjectOnboardingService : IObjectOnboardingService
{
    private readonly ILocalDatabasePath _paths;
    private readonly ILocalDatabaseBootstrapper _bootstrapper;
    private readonly ISyncOutboxService _outbox;

    public SqliteObjectOnboardingService(
        ILocalDatabasePath paths,
        ILocalDatabaseBootstrapper bootstrapper,
        IEquipmentModelCatalogService catalog,
        ISyncOutboxService outbox)
    {
        _paths = paths;
        _bootstrapper = bootstrapper;
        _ = catalog;
        _outbox = outbox;
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
        if (request.Installations is null || request.Installations.Count == 0)
            throw new InvalidOperationException("Добавьте хотя бы одну установку (оборудование).");

        foreach (var draft in request.Installations)
        {
            if (string.IsNullOrWhiteSpace(draft.InstallationLabel))
                throw new InvalidOperationException("Укажите проектный номер установки.");
        }

        await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
        ObjectOnboardingResult onboardingResult;
        await using (var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                var (organizationId, organizationCreated) = await EnsureOrganizationAsync(connection, tx, request, cancellationToken).ConfigureAwait(false);
                var (facilityId, facilityCreated) = await EnsureFacilityAsync(connection, tx, request, organizationId, cancellationToken).ConfigureAwait(false);
                var (systemId, systemCreated) = await EnsureSystemAsync(connection, tx, request, facilityId, cancellationToken).ConfigureAwait(false);

                var primaryEquipmentTypeId = 0;
                var primaryInstallationId = 0;
                var installationsSaved = 0;
                foreach (var draft in request.Installations)
                {
                    var (equipmentTypeId, _) = await EnsureEquipmentTypeAsync(connection, tx, draft, cancellationToken).ConfigureAwait(false);
                    await EnsureSystemEquipmentLinkAsync(connection, tx, systemId, equipmentTypeId, cancellationToken).ConfigureAwait(false);
                    var label = draft.InstallationLabel.Trim();
                    var (installationId, _) = await EnsureInstallationAsync(
                        connection, tx, draft, systemId, equipmentTypeId, label, cancellationToken).ConfigureAwait(false);
                    if (installationsSaved == 0)
                    {
                        primaryEquipmentTypeId = equipmentTypeId;
                        primaryInstallationId = installationId;
                    }

                    installationsSaved++;
                }

                var contactsSaved = 0;
                int? firstContactId = null;
                foreach (var contact in request.Contacts ?? Array.Empty<ObjectOnboardingContactDraft>())
                {
                    if (string.IsNullOrWhiteSpace(contact.LastName) && string.IsNullOrWhiteSpace(contact.FirstName))
                        continue;
                    if (string.IsNullOrWhiteSpace(contact.LastName) || string.IsNullOrWhiteSpace(contact.FirstName))
                        throw new InvalidOperationException("Для контакта укажите фамилию и имя отдельными полями.");

                    var contactId = await InsertContactAsync(
                        connection, tx, organizationId, facilityId, contact, cancellationToken).ConfigureAwait(false);
                    firstContactId ??= contactId;
                    contactsSaved++;
                }

                if (firstContactId is int responsibleId)
                    await TrySetFacilityResponsibleAsync(connection, tx, facilityId, responsibleId, cancellationToken).ConfigureAwait(false);

                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

                onboardingResult = new ObjectOnboardingResult(
                    organizationId,
                    facilityId,
                    systemId,
                    primaryEquipmentTypeId,
                    primaryInstallationId,
                    installationsSaved,
                    contactsSaved,
                    organizationCreated,
                    facilityCreated,
                    systemCreated);
            }
            catch
            {
                try
                {
                    await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                }

                throw;
            }
        }

        try
        {
            await EnqueueHierarchySyncAsync(onboardingResult, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
        }

        return onboardingResult;
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

        var legalFormCode = (request.NewOrganizationLegalFormCode ?? string.Empty).Trim();
        var companyName = (request.NewOrganizationCompanyName ?? string.Empty).Trim();
        if (legalFormCode.Length == 0)
            throw new InvalidOperationException("Укажите юридический статус.");
        if (!OrganizationLegalForm.IsValidCode(legalFormCode))
            throw new InvalidOperationException("Некорректный юридический статус.");
        if (companyName.Length == 0)
            throw new InvalidOperationException("Укажите название компании.");

        using (var find = connection.CreateCommand())
        {
            find.Transaction = tx;
            find.CommandText = """
                SELECT id
                FROM organizations
                WHERE is_active = 1
                  AND legal_form_code = $form
                  AND TRIM(full_name) = $name;
                """;
            find.Parameters.AddWithValue("$form", legalFormCode);
            find.Parameters.AddWithValue("$name", companyName);
            var existing = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (existing is not null)
                return (Convert.ToInt32(existing), false);
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT INTO organizations (full_name, short_name, legal_form_code, is_active)
            VALUES ($name, NULL, $form, 1);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$name", companyName);
        insert.Parameters.AddWithValue("$form", legalFormCode);
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
            throw new InvalidOperationException("Укажите название объекта для акта.");

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

        var contractAddress = (request.ContractAddress ?? string.Empty).Trim();
        var city = (request.AddressCity ?? string.Empty).Trim();
        var street = (request.AddressStreet ?? string.Empty).Trim();
        var building = (request.AddressBuilding ?? string.Empty).Trim();
        if (city.Length == 0 || street.Length == 0 || building.Length == 0)
            throw new InvalidOperationException("Для нового объекта заполните реальный адрес: город, улица и дом.");

        int addressId;
        using (var insertAddress = connection.CreateCommand())
        {
            insertAddress.Transaction = tx;
            insertAddress.CommandText = """
                INSERT INTO organization_addresses (zip_code, country, city, street, building, structure, block)
                VALUES ($zip, 'Россия', $city, $street, $building, $structure, $block);
                SELECT last_insert_rowid();
                """;
            insertAddress.Parameters.AddWithValue("$zip", NullIfEmpty(request.AddressZipCode));
            insertAddress.Parameters.AddWithValue("$city", city);
            insertAddress.Parameters.AddWithValue("$street", street);
            insertAddress.Parameters.AddWithValue("$building", building);
            insertAddress.Parameters.AddWithValue("$structure", NullIfEmpty(request.AddressStructure));
            insertAddress.Parameters.AddWithValue("$block", NullIfEmpty(request.AddressBlock));
            var s = await insertAddress.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            addressId = s is long l ? (int)l : Convert.ToInt32(s);
        }

        using var insertFacility = connection.CreateCommand();
        insertFacility.Transaction = tx;
        insertFacility.CommandText = """
            INSERT INTO facilities (organization_id, name, address_id, contract_address, ui_flow, is_active)
            VALUES ($org, $name, $addr, $contract, 'hierarchical', 1);
            SELECT last_insert_rowid();
            """;
        insertFacility.Parameters.AddWithValue("$org", organizationId);
        insertFacility.Parameters.AddWithValue("$name", facilityName);
        insertFacility.Parameters.AddWithValue("$addr", addressId);
        insertFacility.Parameters.AddWithValue("$contract", NullIfEmpty(contractAddress));
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
        var systemName = DefaultFacilitySystem.Name;

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
            {
                var id = Convert.ToInt32(existing);
                await UpdateSystemDescriptionAsync(connection, tx, id, request.SystemDescription, cancellationToken)
                    .ConfigureAwait(false);
                return (id, false);
            }
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
        insert.Parameters.AddWithValue("$descr", NullIfEmpty(request.SystemDescription));
        var scalar = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return (scalar is long l ? (int)l : Convert.ToInt32(scalar), true);
    }

    private static async Task UpdateSystemDescriptionAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        int systemId,
        string? description,
        CancellationToken cancellationToken)
    {
        var text = (description ?? string.Empty).Trim();
        if (text.Length == 0)
            return;

        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE facility_systems
            SET description = $descr
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", systemId);
        cmd.Parameters.AddWithValue("$descr", text);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(int Id, bool Created)> EnsureEquipmentTypeAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        ObjectOnboardingInstallationDraft draft,
        CancellationToken cancellationToken)
    {
        if (draft.ExistingEquipmentTypeId is int existingId)
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

        var typeName = (draft.NewEquipmentTypeName ?? string.Empty).Trim();
        if (typeName.Length == 0)
            throw new InvalidOperationException("Укажите тип оборудования.");

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
            VALUES ($name, NULL);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$name", typeName);
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

    private async Task<(int Id, bool Created)> EnsureInstallationAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        ObjectOnboardingInstallationDraft draft,
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
                await UpdateInstallationDetailsAsync(connection, tx, id, equipmentTypeId, draft, cancellationToken).ConfigureAwait(false);
                return (id, false);
            }
        }

        var serial = (draft.InstallationSerialNumber ?? string.Empty).Trim();

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT INTO installations (
                system_id,
                equipment_type_id,
                custom_name,
                custom_serial_number,
                is_data_modified,
                is_active
            )
            VALUES ($sid, $eid, $name, $serial, $modified, 1);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$sid", systemId);
        insert.Parameters.AddWithValue("$eid", equipmentTypeId);
        insert.Parameters.AddWithValue("$name", installationLabel);
        insert.Parameters.AddWithValue("$serial", NullIfEmpty(serial));
        insert.Parameters.AddWithValue("$modified", serial.Length > 0 ? 1 : 0);
        var scalar = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var installationId = scalar is long l ? (int)l : Convert.ToInt32(scalar);
        await UpdateInstallationDetailsAsync(connection, tx, installationId, equipmentTypeId, draft, cancellationToken).ConfigureAwait(false);
        return (installationId, true);
    }

    private async Task UpdateInstallationDetailsAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        int installationId,
        int equipmentTypeId,
        ObjectOnboardingInstallationDraft draft,
        CancellationToken cancellationToken)
    {
        var mfg = (draft.InstallationManufacturer ?? string.Empty).Trim();
        var model = (draft.InstallationModel ?? string.Empty).Trim();
        var serial = (draft.InstallationSerialNumber ?? string.Empty).Trim();
        if (mfg.Length == 0 && model.Length == 0 && serial.Length == 0)
            return;

		int? equipmentModelId = null;
		if (mfg.Length > 0 && model.Length > 0)
		{
			equipmentModelId = await SqliteEquipmentModelCatalogService.EnsureModelInTransactionAsync(
				connection, tx, equipmentTypeId, mfg, model, cancellationToken).ConfigureAwait(false);
		}

        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE installations
            SET equipment_model_id = COALESCE($modelId, equipment_model_id),
                custom_model_name = COALESCE(NULLIF($model, ''), custom_model_name),
                custom_serial_number = COALESCE(NULLIF($serial, ''), custom_serial_number),
                is_data_modified = CASE
                    WHEN NULLIF($model, '') IS NOT NULL OR NULLIF($serial, '') IS NOT NULL OR $modelId IS NOT NULL THEN 1
                    ELSE is_data_modified
                END
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$modelId", equipmentModelId.HasValue ? equipmentModelId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$model", model);
        cmd.Parameters.AddWithValue("$serial", serial);
        cmd.Parameters.AddWithValue("$id", installationId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> InsertContactAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        int organizationId,
        int facilityId,
        ObjectOnboardingContactDraft contact,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO organization_employees (
                organization_id, facility_id, first_name, last_name, middle_name,
                position, work_phone, work_email, is_active)
            VALUES (
                $org, $facility, $first, $last, $middle,
                $position, $phone, $email, 1);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$org", organizationId);
        cmd.Parameters.AddWithValue("$facility", facilityId);
        cmd.Parameters.AddWithValue("$first", contact.FirstName.Trim());
        cmd.Parameters.AddWithValue("$last", contact.LastName.Trim());
        cmd.Parameters.AddWithValue("$middle", NullIfEmpty(contact.MiddleName));
        cmd.Parameters.AddWithValue("$position", NullIfEmpty(contact.Position));
        cmd.Parameters.AddWithValue("$phone", NullIfEmpty(contact.Phone));
        cmd.Parameters.AddWithValue("$email", NullIfEmpty(contact.Email));
        var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is long l ? (int)l : Convert.ToInt32(scalar);
    }

    private static async Task TrySetFacilityResponsibleAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        int facilityId,
        int employeeId,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE facilities
            SET responsible_employee_id = COALESCE(responsible_employee_id, $emp)
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$emp", employeeId);
        cmd.Parameters.AddWithValue("$id", facilityId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnqueueHierarchySyncAsync(ObjectOnboardingResult result, CancellationToken cancellationToken)
    {
        var json = await SqliteHierarchySyncPayloadBuilder.BuildAsync(_paths, _bootstrapper, result, cancellationToken)
            .ConfigureAwait(false);
        var payload = System.Text.Json.JsonSerializer.Deserialize<HierarchySyncPayload>(json);
        var uuid = payload?.ClientUuid ?? Guid.NewGuid().ToString();
        await _outbox.EnqueueAsync(new SyncOutboxEnqueueRequest("hierarchy", uuid, "upsert", json), cancellationToken)
            .ConfigureAwait(false);
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
