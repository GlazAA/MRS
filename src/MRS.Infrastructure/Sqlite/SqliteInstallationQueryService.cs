using Microsoft.Data.Sqlite;
using MRS.Application.Facilities;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteInstallationQueryService : IInstallationQueryService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteInstallationQueryService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<IReadOnlyList<InstallationListItem>> GetForSystemAndEquipmentAsync(
		int facilitySystemId,
		int equipmentTypeId,
		CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT i.id,
				COALESCE(NULLIF(TRIM(i.custom_name), ''), 'Установка #' || CAST(i.id AS TEXT))
			FROM installations i
			WHERE i.system_id = $sid AND i.equipment_type_id = $eid AND i.is_active = 1
			ORDER BY i.id;
			""";
		cmd.Parameters.AddWithValue("$sid", facilitySystemId);
		cmd.Parameters.AddWithValue("$eid", equipmentTypeId);

		var list = new List<InstallationListItem>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			list.Add(new InstallationListItem(reader.GetInt32(0), reader.GetString(1)));

		return list;
	}

	public async Task<IReadOnlyList<InstallationFilterItem>> ListActiveForFiltersAsync(
		CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT
				TRIM(f.name) AS facility_name,
				COALESCE(NULLIF(TRIM(i.custom_name), ''), CAST(i.id AS TEXT)) AS installation_label
			FROM installations i
			INNER JOIN facility_systems fs ON fs.id = i.system_id
			INNER JOIN facilities f ON f.id = fs.facility_id
			WHERE i.is_active = 1
			  AND f.is_active = 1
			  AND TRIM(f.name) <> ''
			ORDER BY f.name COLLATE NOCASE, installation_label COLLATE NOCASE;
			""";

		var list = new List<InstallationFilterItem>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var facility = reader.GetString(0).Trim();
			var label = reader.GetString(1).Trim();
			if (facility.Length == 0 || label.Length == 0)
				continue;
			var key = facility + "|" + label;
			if (!seen.Add(key))
				continue;
			list.Add(new InstallationFilterItem(facility, label));
		}

		return list;
	}
}
