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
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT DISTINCT TRIM(manufacturer)
			FROM equipment_models
			WHERE equipment_type_id = $et
			  AND manufacturer IS NOT NULL
			  AND TRIM(manufacturer) <> ''
			ORDER BY TRIM(manufacturer);
			""";
		cmd.Parameters.AddWithValue("$et", equipmentTypeId);

		var list = new List<string>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			list.Add(reader.GetString(0));
		return list;
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
			SELECT id, TRIM(manufacturer), name
			FROM equipment_models
			WHERE equipment_type_id = $et
			  AND TRIM(manufacturer) = $mfg
			ORDER BY name;
			""";
		cmd.Parameters.AddWithValue("$et", equipmentTypeId);
		cmd.Parameters.AddWithValue("$mfg", mfg);

		var list = new List<EquipmentModelListItem>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			list.Add(new EquipmentModelListItem(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
		return list;
	}

	public async Task<bool> HasAnyModelsAsync(int equipmentTypeId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT COUNT(1)
			FROM equipment_models
			WHERE equipment_type_id = $et;
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
		var mfg = (manufacturer ?? string.Empty).Trim();
		var name = (modelName ?? string.Empty).Trim();
		if (mfg.Length == 0 || name.Length == 0)
			throw new InvalidOperationException("Укажите производителя и модель.");

		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);

		using (var find = connection.CreateCommand())
		{
			find.CommandText = """
				SELECT id
				FROM equipment_models
				WHERE equipment_type_id = $et
				  AND TRIM(manufacturer) = $mfg
				  AND TRIM(name) = $name
				LIMIT 1;
				""";
			find.Parameters.AddWithValue("$et", equipmentTypeId);
			find.Parameters.AddWithValue("$mfg", mfg);
			find.Parameters.AddWithValue("$name", name);
			var existing = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (existing is not null)
				return Convert.ToInt32(existing);
		}

		using var insert = connection.CreateCommand();
		insert.CommandText = """
			INSERT INTO equipment_models (equipment_type_id, manufacturer, name)
			VALUES ($et, $mfg, $name);
			SELECT last_insert_rowid();
			""";
		insert.Parameters.AddWithValue("$et", equipmentTypeId);
		insert.Parameters.AddWithValue("$mfg", mfg);
		insert.Parameters.AddWithValue("$name", name);
		var scalar = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is long l ? (int)l : Convert.ToInt32(scalar ?? 0);
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
				COALESCE(em.name, i.custom_model_name),
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
			// legacy custom_model_name without catalog link
			return new InstallationEquipmentModelInfo(installationId, null, modelName, modelId);
		}

		return new InstallationEquipmentModelInfo(
			reader.GetInt32(0),
			string.IsNullOrWhiteSpace(manufacturer) ? null : manufacturer,
			string.IsNullOrWhiteSpace(modelName) ? null : modelName,
			modelId);
	}
}
