using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Application.Users;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteEngineerDirectoryService : IEngineerDirectoryService
{
	private const string OfflinePasswordPlaceholder = "$2a$11$OfflinePlaceholderHashNotForAuth";

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
		return await QueryAsync(connection, null, cancellationToken).ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<EngineerDirectoryEntry>> SearchAsync(string? query, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		return await QueryAsync(connection, query, cancellationToken).ConfigureAwait(false);
	}

	public async Task<EngineerDirectoryEntry> CreateAsync(CreateEngineerRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		var last = (request.LastName ?? string.Empty).Trim();
		var first = (request.FirstName ?? string.Empty).Trim();
		var middle = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim();
		if (last.Length == 0)
			throw new InvalidOperationException("Укажите фамилию инженера.");
		if (first.Length == 0)
			throw new InvalidOperationException("Укажите имя инженера.");

		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);

		int roleId;
		using (var roleCmd = connection.CreateCommand())
		{
			roleCmd.CommandText = "SELECT id FROM user_roles WHERE role_name = 'Инженер' LIMIT 1;";
			var roleObj = await roleCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (roleObj is null || roleObj is DBNull)
				throw new InvalidOperationException("Роль «Инженер» не найдена в справочнике.");
			roleId = Convert.ToInt32(roleObj);
		}

		var login = $"eng_{Guid.NewGuid():N}"[..16];
		using var insert = connection.CreateCommand();
		insert.CommandText = """
			INSERT INTO users (user_role_id, first_name, last_name, middle_name, login, password_hash, is_active)
			VALUES ($role, $first, $last, $middle, $login, $hash, 1);
			SELECT last_insert_rowid();
			""";
		insert.Parameters.AddWithValue("$role", roleId);
		insert.Parameters.AddWithValue("$first", first);
		insert.Parameters.AddWithValue("$last", last);
		insert.Parameters.AddWithValue("$middle", (object?)middle ?? DBNull.Value);
		insert.Parameters.AddWithValue("$login", login);
		insert.Parameters.AddWithValue("$hash", OfflinePasswordPlaceholder);

		var idObj = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		var id = Convert.ToInt32(idObj);
		return new EngineerDirectoryEntry(id, FormatLabel(last, first, middle));
	}

	private static async Task<IReadOnlyList<EngineerDirectoryEntry>> QueryAsync(
		SqliteConnection connection,
		string? query,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		var q = (query ?? string.Empty).Trim();
		if (q.Length == 0)
		{
			cmd.CommandText = """
				SELECT u.id, u.first_name, u.last_name, COALESCE(u.middle_name, '')
				FROM users u
				INNER JOIN user_roles r ON r.id = u.user_role_id
				WHERE u.is_active = 1 AND r.role_name = 'Инженер'
				ORDER BY u.last_name, u.first_name
				LIMIT 40;
				""";
		}
		else
		{
			cmd.CommandText = """
				SELECT u.id, u.first_name, u.last_name, COALESCE(u.middle_name, '')
				FROM users u
				INNER JOIN user_roles r ON r.id = u.user_role_id
				WHERE u.is_active = 1 AND r.role_name = 'Инженер'
				  AND (
				    u.last_name LIKE $q OR u.first_name LIKE $q OR COALESCE(u.middle_name, '') LIKE $q
				    OR (u.last_name || ' ' || u.first_name || ' ' || COALESCE(u.middle_name, '')) LIKE $q
				    OR (u.last_name || ' ' || u.first_name) LIKE $q
				  )
				ORDER BY u.last_name, u.first_name
				LIMIT 40;
				""";
			cmd.Parameters.AddWithValue("$q", "%" + q + "%");
		}

		var list = new List<EngineerDirectoryEntry>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var id = reader.GetInt32(0);
			var first = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
			var last = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
			var middle = reader.GetString(3);
			list.Add(new EngineerDirectoryEntry(id, FormatLabel(last, first, middle)));
		}

		return list;
	}

	private static string FormatLabel(string last, string first, string? middle)
	{
		var label = string.IsNullOrWhiteSpace(middle)
			? $"{last} {first}".Trim()
			: $"{last} {first} {middle}".Trim();
		return label.Length == 0 ? "Инженер" : label;
	}
}
