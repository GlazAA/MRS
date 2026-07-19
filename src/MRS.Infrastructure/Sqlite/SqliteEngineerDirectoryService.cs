using Microsoft.Data.Sqlite;
using MRS.Application.Security;
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
		return await QueryActiveEngineersAsync(connection, null, cancellationToken).ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<EngineerDirectoryEntry>> SearchAsync(string? query, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		return await QueryActiveEngineersAsync(connection, query, cancellationToken).ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<EngineerAdminEntry>> ListAllForAdminAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT u.id, u.last_name, u.first_name, u.middle_name, r.role_name, u.is_active
			FROM users u
			INNER JOIN user_roles r ON r.id = u.user_role_id
			WHERE r.role_name IN ($eng, $mgr, $adm)
			ORDER BY u.is_active DESC, r.role_name, u.last_name, u.first_name;
			""";
		cmd.Parameters.AddWithValue("$eng", UserRoleNames.Engineer);
		cmd.Parameters.AddWithValue("$mgr", UserRoleNames.Manager);
		cmd.Parameters.AddWithValue("$adm", UserRoleNames.DbAdministrator);

		var list = new List<EngineerAdminEntry>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			list.Add(ReadAdminEntry(reader));
		}

		return list;
	}

	public async Task<EngineerDirectoryEntry> CreateAsync(CreateEngineerRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		var (last, first, middle) = NormalizeName(request);
		var roleName = UserRoleNames.NormalizeOrDefault(request.RoleName);

		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		var roleId = await ResolveRoleIdAsync(connection, roleName, cancellationToken).ConfigureAwait(false);

		var login = $"usr_{Guid.NewGuid():N}"[..16];
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

	public async Task UpdateAsync(int userId, CreateEngineerRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (userId <= 0)
			throw new ArgumentOutOfRangeException(nameof(userId));

		var (last, first, middle) = NormalizeName(request);
		var roleName = UserRoleNames.NormalizeOrDefault(request.RoleName);

		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		await EnsureStaffUserAsync(connection, userId, cancellationToken).ConfigureAwait(false);
		var roleId = await ResolveRoleIdAsync(connection, roleName, cancellationToken).ConfigureAwait(false);

		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			UPDATE users
			SET first_name = $first,
			    last_name = $last,
			    middle_name = $middle,
			    user_role_id = $role,
			    updated_at = datetime('now')
			WHERE id = $id;
			""";
		cmd.Parameters.AddWithValue("$first", first);
		cmd.Parameters.AddWithValue("$last", last);
		cmd.Parameters.AddWithValue("$middle", (object?)middle ?? DBNull.Value);
		cmd.Parameters.AddWithValue("$role", roleId);
		cmd.Parameters.AddWithValue("$id", userId);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default)
	{
		if (userId <= 0)
			throw new ArgumentOutOfRangeException(nameof(userId));

		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		await EnsureStaffUserAsync(connection, userId, cancellationToken).ConfigureAwait(false);

		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			UPDATE users
			SET is_active = $active,
			    updated_at = datetime('now')
			WHERE id = $id;
			""";
		cmd.Parameters.AddWithValue("$active", isActive ? 1 : 0);
		cmd.Parameters.AddWithValue("$id", userId);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task EnsureStaffUserAsync(
		SqliteConnection connection,
		int userId,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT COUNT(1)
			FROM users u
			INNER JOIN user_roles r ON r.id = u.user_role_id
			WHERE u.id = $id AND r.role_name IN ($eng, $mgr, $adm);
			""";
		cmd.Parameters.AddWithValue("$id", userId);
		cmd.Parameters.AddWithValue("$eng", UserRoleNames.Engineer);
		cmd.Parameters.AddWithValue("$mgr", UserRoleNames.Manager);
		cmd.Parameters.AddWithValue("$adm", UserRoleNames.DbAdministrator);
		var ok = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
		if (!ok)
			throw new InvalidOperationException("Сотрудник не найден.");
	}

	private static async Task<int> ResolveRoleIdAsync(
		SqliteConnection connection,
		string roleName,
		CancellationToken cancellationToken)
	{
		using var roleCmd = connection.CreateCommand();
		roleCmd.CommandText = "SELECT id FROM user_roles WHERE role_name = $name LIMIT 1;";
		roleCmd.Parameters.AddWithValue("$name", roleName);
		var roleObj = await roleCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		if (roleObj is null || roleObj is DBNull)
			throw new InvalidOperationException($"Роль «{roleName}» не найдена в справочнике.");
		return Convert.ToInt32(roleObj);
	}

	private static (string Last, string First, string? Middle) NormalizeName(CreateEngineerRequest request)
	{
		var last = (request.LastName ?? string.Empty).Trim();
		var first = (request.FirstName ?? string.Empty).Trim();
		var middle = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim();
		if (last.Length == 0)
			throw new InvalidOperationException("Укажите фамилию.");
		if (first.Length == 0)
			throw new InvalidOperationException("Укажите имя.");
		return (last, first, middle);
	}

	private static async Task<IReadOnlyList<EngineerDirectoryEntry>> QueryActiveEngineersAsync(
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
				WHERE u.is_active = 1 AND r.role_name = $eng
				ORDER BY u.last_name, u.first_name
				LIMIT 200;
				""";
			cmd.Parameters.AddWithValue("$eng", UserRoleNames.Engineer);
		}
		else
		{
			cmd.CommandText = """
				SELECT u.id, u.first_name, u.last_name, COALESCE(u.middle_name, '')
				FROM users u
				INNER JOIN user_roles r ON r.id = u.user_role_id
				WHERE u.is_active = 1 AND r.role_name = $eng
				  AND (
				    u.last_name LIKE $q OR u.first_name LIKE $q OR COALESCE(u.middle_name, '') LIKE $q
				    OR (u.last_name || ' ' || u.first_name || ' ' || COALESCE(u.middle_name, '')) LIKE $q
				    OR (u.last_name || ' ' || u.first_name) LIKE $q
				  )
				ORDER BY u.last_name, u.first_name
				LIMIT 40;
				""";
			cmd.Parameters.AddWithValue("$eng", UserRoleNames.Engineer);
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

	private static EngineerAdminEntry ReadAdminEntry(SqliteDataReader reader) =>
		new(
			reader.GetInt32(0),
			reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
			reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
			reader.IsDBNull(3) ? null : reader.GetString(3),
			reader.IsDBNull(4) ? UserRoleNames.Engineer : reader.GetString(4),
			reader.GetInt32(5) != 0);

	private static string FormatLabel(string last, string first, string? middle)
	{
		var label = string.IsNullOrWhiteSpace(middle)
			? $"{last} {first}".Trim()
			: $"{last} {first} {middle}".Trim();
		return label.Length == 0 ? "Сотрудник" : label;
	}
}
