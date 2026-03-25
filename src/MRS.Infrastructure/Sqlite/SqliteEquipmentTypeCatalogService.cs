using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteEquipmentTypeCatalogService : IEquipmentTypeCatalogService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteEquipmentTypeCatalogService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<IReadOnlyList<EquipmentTypeListItem>> GetForSystemAsync(int facilitySystemId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT et.id, et.type_name, et.code
			FROM equipment_types et
			INNER JOIN system_equipment_types st ON st.equipment_type_id = et.id
			WHERE st.system_id = $sid
			ORDER BY et.type_name;
			""";
		cmd.Parameters.AddWithValue("$sid", facilitySystemId);

		var list = new List<EquipmentTypeListItem>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var id = reader.GetInt32(0);
			var name = reader.GetString(1);
			string? code = reader.IsDBNull(2) ? null : reader.GetString(2);
			list.Add(new EquipmentTypeListItem(id, name, code));
		}

		return list;
	}
}
