using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteChecklistSummaryService : IChecklistSummaryService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteChecklistSummaryService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<IReadOnlyList<ChecklistSummaryRow>> GetForSystemAsync(int facilitySystemId, int limit = 25, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT c.id, et.type_name, c.start_at, c.status
			FROM checklists c
			INNER JOIN installations i ON c.installation_id = i.id
			INNER JOIN equipment_types et ON i.equipment_type_id = et.id
			WHERE i.system_id = $sid AND c.is_active = 1
			ORDER BY CASE WHEN c.start_at IS NULL THEN 1 ELSE 0 END, c.start_at DESC, c.id DESC
			LIMIT $lim;
			""";
		cmd.Parameters.AddWithValue("$sid", facilitySystemId);
		cmd.Parameters.AddWithValue("$lim", limit);

		var list = new List<ChecklistSummaryRow>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var id = reader.GetInt32(0);
			var typeName = reader.GetString(1);
			DateTimeOffset? started = null;
			if (!reader.IsDBNull(2))
			{
				var raw = reader.GetString(2);
				if (DateTimeOffset.TryParse(raw, out var dto))
					started = dto;
			}

			var status = reader.GetString(3);
			list.Add(new ChecklistSummaryRow(id, typeName, started, status));
		}

		return list;
	}
}
