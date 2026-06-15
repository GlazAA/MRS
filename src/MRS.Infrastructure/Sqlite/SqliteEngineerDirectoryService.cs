using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Application.Users;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteEngineerDirectoryService : IEngineerDirectoryService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteEngineerDirectoryService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<IReadOnlyList<EngineerDirectoryEntry>> ListActiveAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT u.id, u.first_name, u.last_name, COALESCE(u.middle_name, '')
			FROM users u
			INNER JOIN user_roles r ON r.id = u.user_role_id
			WHERE u.is_active = 1 AND r.role_name = 'Инженер'
			ORDER BY u.last_name, u.first_name;
			""";

		var list = new List<EngineerDirectoryEntry>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var id = reader.GetInt32(0);
			var first = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
			var last = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
			var middle = reader.GetString(3);
			var label = string.IsNullOrWhiteSpace(middle)
				? $"{last} {first}".Trim()
				: $"{last} {first} {middle}".Trim();
			if (label.Length == 0)
				label = $"Инженер #{id}";
			list.Add(new EngineerDirectoryEntry(id, label));
		}

		return list;
	}
}
