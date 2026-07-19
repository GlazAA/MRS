using Microsoft.Data.Sqlite;
using MRS.Application.Facilities;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteEquipmentModelCatalogService : IEquipmentModelCatalogService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteEquipmentModelCatalogService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<IReadOnlyList<string>> GetManufacturersAsync(int equipmentTypeId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);

		// Общий справочник: все производители из БД + ответы КЛ (не фильтруем по объекту/компании).
		// equipmentTypeId влияет только на порядок: сначала бренды этого типа.
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT TRIM(manufacturer) AS name,
			       CASE WHEN equipment_type_id = $et THEN 0 ELSE 1 END AS rank
			FROM equipment_models
			WHERE manufacturer IS NOT NULL
			  AND TRIM(manufacturer) <> ''
			UNION ALL
			SELECT TRIM(cr.text_response) AS name,
			       CASE WHEN i.equipment_type_id = $et THEN 0 ELSE 1 END AS rank
			FROM checklist_responses cr
			JOIN checklist_template_items cti ON cti.id = cr.checklist_template_item_id
			JOIN checklists c ON c.id = cr.checklist_id
			JOIN installations i ON i.id = c.installation_id
			WHERE cr.text_response IS NOT NULL
			  AND TRIM(cr.text_response) <> ''
			  AND length(cti.field_code) >= 13
			  AND lower(substr(cti.field_code, -13)) = '_manufacturer'
			ORDER BY rank, name COLLATE NOCASE;
			""";
		cmd.Parameters.AddWithValue("$et", equipmentTypeId);

		var byKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var name = reader.GetString(0).Trim();
			if (name.Length == 0)
				continue;
			byKey.TryAdd(name, name);
		}

		return byKey.Values.OrderBy(v => v, StringComparer.CurrentCultureIgnoreCase).ToList();
	}

	public async Task<IReadOnlyList<EquipmentModelListItem>> GetModelsAsync(
		int equipmentTypeId,
		string manufacturer,
		CancellationToken cancellationToken = default)
	{
		var mfg = (manufacturer ?? string.Empty).Trim();
		if (mfg.Length == 0)
			return [];

		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT id, TRIM(manufacturer), TRIM(name),
			       CASE WHEN equipment_type_id = $et THEN 0 ELSE 1 END AS rank
			FROM equipment_models
			WHERE lower(TRIM(manufacturer)) = lower($mfg)
			  AND name IS NOT NULL
			  AND TRIM(name) <> ''
			UNION ALL
			SELECT 0 AS id,
			       $mfg AS manufacturer,
			       TRIM(cr.text_response) AS name,
			       CASE WHEN i.equipment_type_id = $et THEN 0 ELSE 1 END AS rank
			FROM checklist_responses cr
			JOIN checklist_template_items cti ON cti.id = cr.checklist_template_item_id
			JOIN checklists c ON c.id = cr.checklist_id
			JOIN installations i ON i.id = c.installation_id
			WHERE cr.text_response IS NOT NULL
			  AND TRIM(cr.text_response) <> ''
			  AND length(cti.field_code) >= 6
			  AND lower(substr(cti.field_code, -6)) = '_model'
			  AND lower(substr(cti.field_code, -13)) <> '_manufacturer'
			  AND EXISTS (
			      SELECT 1
			      FROM checklist_responses cr_mfg
			      JOIN checklist_template_items cti_mfg ON cti_mfg.id = cr_mfg.checklist_template_item_id
			      WHERE cr_mfg.checklist_id = cr.checklist_id
			        AND lower(cti_mfg.field_code) = lower(
			            substr(cti.field_code, 1, length(cti.field_code) - 6) || '_manufacturer')
			        AND lower(TRIM(cr_mfg.text_response)) = lower($mfg)
			  )
			ORDER BY rank, name COLLATE NOCASE;
			""";
		cmd.Parameters.AddWithValue("$et", equipmentTypeId);
		cmd.Parameters.AddWithValue("$mfg", mfg);

		// Без дубликатов по имени: предпочитаем строку своего типа (rank=0, раньше в выборке).
		var byKey = new Dictionary<string, EquipmentModelListItem>(StringComparer.OrdinalIgnoreCase);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var name = reader.GetString(2).Trim();
			if (name.Length == 0)
				continue;
			var item = new EquipmentModelListItem(reader.GetInt32(0), reader.GetString(1).Trim(), name);
			byKey.TryAdd(item.Name, item);
		}

		return byKey.Values.OrderBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
	}

	public async Task<bool> HasAnyModelsAsync(int equipmentTypeId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT COUNT(1)
			FROM equipment_models
			WHERE equipment_type_id = $et
			  AND name IS NOT NULL
			  AND TRIM(name) <> '';
			""";
		cmd.Parameters.AddWithValue("$et", equipmentTypeId);
		var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
		return count > 0;
	}

	public async Task<int> EnsureModelAsync(
		int equipmentTypeId,
		string manufacturer,
		string modelName,
		CancellationToken cancellationToken = default)
	{
		var entry = await EnsureModelEntryAsync(equipmentTypeId, manufacturer, modelName, cancellationToken)
			.ConfigureAwait(false);
		return entry.Id;
	}

	public async Task<EquipmentModelListItem> EnsureModelEntryAsync(
		int equipmentTypeId,
		string manufacturer,
		string modelName,
		CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		return await EnsureModelEntryInTransactionAsync(connection, null, equipmentTypeId, manufacturer, modelName, cancellationToken)
			.ConfigureAwait(false);
	}

	public async Task EnsureManufacturerAsync(
		int equipmentTypeId,
		string manufacturer,
		CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		await EnsureManufacturerInTransactionAsync(connection, null, equipmentTypeId, manufacturer, cancellationToken)
			.ConfigureAwait(false);
	}

	internal static async Task<int> EnsureModelInTransactionAsync(
		SqliteConnection connection,
		SqliteTransaction? tx,
		int equipmentTypeId,
		string manufacturer,
		string modelName,
		CancellationToken cancellationToken)
	{
		var entry = await EnsureModelEntryInTransactionAsync(connection, tx, equipmentTypeId, manufacturer, modelName, cancellationToken)
			.ConfigureAwait(false);
		return entry.Id;
	}

	internal static async Task EnsureManufacturerInTransactionAsync(
		SqliteConnection connection,
		SqliteTransaction? tx,
		int equipmentTypeId,
		string manufacturer,
		CancellationToken cancellationToken)
	{
		var mfg = (manufacturer ?? string.Empty).Trim();
		if (mfg.Length == 0)
			return;

		var canonicalMfg = await FindCanonicalManufacturerAsync(connection, tx, equipmentTypeId, mfg, cancellationToken)
			.ConfigureAwait(false);
		if (canonicalMfg is not null)
			return;

		// Глобально уже есть такое написание — переиспользуем канон из любого типа.
		canonicalMfg = await FindCanonicalManufacturerGlobalAsync(connection, tx, mfg, cancellationToken)
			.ConfigureAwait(false) ?? mfg;

		using var insert = connection.CreateCommand();
		insert.Transaction = tx;
		insert.CommandText = """
			INSERT INTO equipment_models (equipment_type_id, manufacturer, name)
			VALUES ($et, $mfg, '');
			""";
		insert.Parameters.AddWithValue("$et", equipmentTypeId);
		insert.Parameters.AddWithValue("$mfg", canonicalMfg);
		await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	internal static async Task<EquipmentModelListItem> EnsureModelEntryInTransactionAsync(
		SqliteConnection connection,
		SqliteTransaction? tx,
		int equipmentTypeId,
		string manufacturer,
		string modelName,
		CancellationToken cancellationToken)
	{
		var mfg = (manufacturer ?? string.Empty).Trim();
		var name = (modelName ?? string.Empty).Trim();
		if (mfg.Length == 0 || name.Length == 0)
			throw new InvalidOperationException("Укажите производителя и модель.");

		using (var find = connection.CreateCommand())
		{
			find.Transaction = tx;
			find.CommandText = """
				SELECT id, TRIM(manufacturer), TRIM(name)
				FROM equipment_models
				WHERE equipment_type_id = $et
				  AND lower(TRIM(manufacturer)) = lower($mfg)
				  AND lower(TRIM(name)) = lower($name)
				ORDER BY id
				LIMIT 1;
				""";
			find.Parameters.AddWithValue("$et", equipmentTypeId);
			find.Parameters.AddWithValue("$mfg", mfg);
			find.Parameters.AddWithValue("$name", name);
			await using var reader = await find.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				return new EquipmentModelListItem(
					reader.GetInt32(0),
					reader.GetString(1),
					reader.GetString(2));
			}
		}

		var canonicalMfg = await FindCanonicalManufacturerAsync(connection, tx, equipmentTypeId, mfg, cancellationToken)
			.ConfigureAwait(false)
			?? await FindCanonicalManufacturerGlobalAsync(connection, tx, mfg, cancellationToken).ConfigureAwait(false)
			?? mfg;

		using var insert = connection.CreateCommand();
		insert.Transaction = tx;
		insert.CommandText = """
			INSERT INTO equipment_models (equipment_type_id, manufacturer, name)
			VALUES ($et, $mfg, $name);
			SELECT last_insert_rowid();
			""";
		insert.Parameters.AddWithValue("$et", equipmentTypeId);
		insert.Parameters.AddWithValue("$mfg", canonicalMfg);
		insert.Parameters.AddWithValue("$name", name);
		var scalar = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		var id = scalar is long l ? (int)l : Convert.ToInt32(scalar ?? 0);
		return new EquipmentModelListItem(id, canonicalMfg, name);
	}

	private static async Task<string?> FindCanonicalManufacturerAsync(
		SqliteConnection connection,
		SqliteTransaction? tx,
		int equipmentTypeId,
		string manufacturer,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.Transaction = tx;
		cmd.CommandText = """
			SELECT TRIM(manufacturer)
			FROM equipment_models
			WHERE equipment_type_id = $et
			  AND lower(TRIM(manufacturer)) = lower($mfg)
			ORDER BY id
			LIMIT 1;
			""";
		cmd.Parameters.AddWithValue("$et", equipmentTypeId);
		cmd.Parameters.AddWithValue("$mfg", manufacturer);
		var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar as string;
	}

	private static async Task<string?> FindCanonicalManufacturerGlobalAsync(
		SqliteConnection connection,
		SqliteTransaction? tx,
		string manufacturer,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.Transaction = tx;
		cmd.CommandText = """
			SELECT TRIM(manufacturer)
			FROM equipment_models
			WHERE lower(TRIM(manufacturer)) = lower($mfg)
			ORDER BY id
			LIMIT 1;
			""";
		cmd.Parameters.AddWithValue("$mfg", manufacturer);
		var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar as string;
	}

	public async Task<InstallationEquipmentModelInfo?> GetInstallationModelAsync(
		int installationId,
		CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT
				i.id,
				em.manufacturer,
				COALESCE(NULLIF(TRIM(em.name), ''), i.custom_model_name),
				i.equipment_model_id
			FROM installations i
			LEFT JOIN equipment_models em ON em.id = i.equipment_model_id
			WHERE i.id = $id AND i.is_active = 1;
			""";
		cmd.Parameters.AddWithValue("$id", installationId);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			return null;

		var modelId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
		var manufacturer = reader.IsDBNull(1) ? null : reader.GetString(1)?.Trim();
		var modelName = reader.IsDBNull(2) ? null : reader.GetString(2)?.Trim();

		if (string.IsNullOrWhiteSpace(manufacturer) && !string.IsNullOrWhiteSpace(modelName))
		{
			return new InstallationEquipmentModelInfo(installationId, null, modelName, modelId);
		}

		return new InstallationEquipmentModelInfo(
			reader.GetInt32(0),
			string.IsNullOrWhiteSpace(manufacturer) ? null : manufacturer,
			string.IsNullOrWhiteSpace(modelName) ? null : modelName,
			modelId);
	}
}
