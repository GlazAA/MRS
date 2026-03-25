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
}
