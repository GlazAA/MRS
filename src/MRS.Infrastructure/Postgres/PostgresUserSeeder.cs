using Npgsql;

namespace MRS.Infrastructure.Postgres;

internal static class PostgresUserSeeder
{
	internal static async Task EnsureDemoUsersAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		await EnsureRoleAsync(connection, 1, "Инженер", cancellationToken).ConfigureAwait(false);
		await EnsureRoleAsync(connection, 2, "Менеджер", cancellationToken).ConfigureAwait(false);
		await EnsureRoleAsync(connection, 3, "Администратор БД", cancellationToken).ConfigureAwait(false);

		await UpsertUserAsync(connection, 1, 1, "Демо", "Инженер", null, "demo", "demo123", cancellationToken).ConfigureAwait(false);
		await UpsertUserAsync(connection, 2, 3, "Демо", "Администратор БД", null, "dbadmin", "admin123", cancellationToken).ConfigureAwait(false);
		await UpsertUserAsync(connection, 3, 1, "Сергей", "Николаев", "Павлович", "engineer2", "demo123", cancellationToken).ConfigureAwait(false);
		await UpsertUserAsync(connection, 4, 1, "Ольга", "Морозова", "Викторовна", "engineer3", "demo123", cancellationToken).ConfigureAwait(false);
	}

	private static async Task EnsureRoleAsync(NpgsqlConnection connection, long id, string roleName, CancellationToken cancellationToken)
	{
		await using var cmd = new NpgsqlCommand("""
			INSERT INTO user_roles (id, role_name) VALUES (@id, @name)
			ON CONFLICT (role_name) DO NOTHING;
			""", connection);
		cmd.Parameters.AddWithValue("id", id);
		cmd.Parameters.AddWithValue("name", roleName);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task UpsertUserAsync(
		NpgsqlConnection connection,
		long id,
		long roleId,
		string firstName,
		string lastName,
		string? middleName,
		string login,
		string password,
		CancellationToken cancellationToken)
	{
		var hash = BCrypt.Net.BCrypt.HashPassword(password);
		await using var cmd = new NpgsqlCommand("""
			INSERT INTO users (id, user_role_id, first_name, last_name, middle_name, login, password_hash, is_active)
			VALUES (@id, @role, @fn, @ln, @mn, @login, @hash, TRUE)
			ON CONFLICT (login) DO UPDATE SET
			    user_role_id = EXCLUDED.user_role_id,
			    first_name = EXCLUDED.first_name,
			    last_name = EXCLUDED.last_name,
			    middle_name = EXCLUDED.middle_name,
			    password_hash = EXCLUDED.password_hash,
			    is_active = TRUE;
			""", connection);
		cmd.Parameters.AddWithValue("id", id);
		cmd.Parameters.AddWithValue("role", roleId);
		cmd.Parameters.AddWithValue("fn", firstName);
		cmd.Parameters.AddWithValue("ln", lastName);
		cmd.Parameters.AddWithValue("mn", middleName ?? (object)DBNull.Value);
		cmd.Parameters.AddWithValue("login", login);
		cmd.Parameters.AddWithValue("hash", hash);
		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}
}
