using System.Globalization;
using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteChecklistManagementService : IChecklistManagementService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteChecklistManagementService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<IReadOnlyList<ChecklistManagementRow>> GetAllAsync(
		CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);

		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT
				c.id,
				c.start_at,
				COALESCE(o.short_name, o.full_name) AS organization_name,
				f.name AS facility_name,
				et.type_name AS equipment_type_name,
				COALESCE(NULLIF(TRIM(i.custom_name), ''), CAST(i.id AS TEXT)) AS installation_label,
				mt.type_name AS maintenance_type_name,
				c.status
			FROM checklists c
			INNER JOIN installations i ON i.id = c.installation_id
			INNER JOIN equipment_types et ON et.id = i.equipment_type_id
			INNER JOIN facility_systems fs ON fs.id = i.system_id
			INNER JOIN facilities f ON f.id = fs.facility_id
			INNER JOIN organizations o ON o.id = f.organization_id
			INNER JOIN maintenance_types mt ON mt.id = c.maintenance_type_id
			WHERE c.is_active = 1
			ORDER BY
				COALESCE(o.short_name, o.full_name),
				f.name,
				et.type_name,
				COALESCE(NULLIF(TRIM(i.custom_name), ''), CAST(i.id AS TEXT));
			""";

		var list = new List<ChecklistManagementRow>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var id = reader.GetInt32(0);
			DateTimeOffset? startedAt = null;
			if (!reader.IsDBNull(1))
			{
				var raw = reader.GetString(1);
				if (DateTimeOffset.TryParse(raw, out var dto))
					startedAt = dto;
			}

			list.Add(new ChecklistManagementRow(
				id,
				startedAt,
				reader.GetString(2),
				reader.GetString(3),
				reader.GetString(4),
				reader.GetString(5),
				reader.GetString(6),
				reader.GetString(7)));
		}

		return list;
	}
}

