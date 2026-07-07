using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MRS.Application.Security;
using Npgsql;

namespace MRS.Infrastructure.Postgres;

public sealed class PostgresAuthService
{
	private readonly PostgresConnectionFactory _factory;
	private readonly string _jwtKey;
	private readonly string _jwtIssuer;
	private readonly string _jwtAudience;

	public PostgresAuthService(PostgresConnectionFactory factory, string jwtKey, string jwtIssuer, string jwtAudience)
	{
		_factory = factory;
		_jwtKey = jwtKey;
		_jwtIssuer = jwtIssuer;
		_jwtAudience = jwtAudience;
	}

	public async Task<AuthLoginResult> LoginAsync(string login, string password, CancellationToken cancellationToken = default)
	{
		var trimmedLogin = (login ?? string.Empty).Trim();
		if (trimmedLogin.Length == 0)
			return new AuthLoginResult(false, "Укажите логин.", null, null, null, null);

		await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var cmd = new NpgsqlCommand("""
			SELECT u.id, u.first_name, u.last_name, u.middle_name, u.password_hash, u.is_active, r.role_name
			FROM users u
			INNER JOIN user_roles r ON r.id = u.user_role_id
			WHERE u.login = @login
			LIMIT 1;
			""", connection);
		cmd.Parameters.AddWithValue("login", trimmedLogin);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			return new AuthLoginResult(false, "Неверный логин или пароль.", null, null, null, null);

		var userId = reader.GetInt64(0);
		var firstName = reader.IsDBNull(1) ? "" : reader.GetString(1);
		var lastName = reader.IsDBNull(2) ? "" : reader.GetString(2);
		var middleName = reader.IsDBNull(3) ? "" : reader.GetString(3);
		var hash = reader.GetString(4);
		var isActive = reader.GetBoolean(5);
		var roleName = reader.GetString(6);

		if (!isActive)
			return new AuthLoginResult(false, "Учётная запись отключена.", null, null, null, null);

		if (!BCrypt.Net.BCrypt.Verify(password ?? string.Empty, hash))
			return new AuthLoginResult(false, "Неверный логин или пароль.", null, null, null, null);

		var displayName = string.Join(' ', new[] { lastName, firstName, middleName }.Where(s => !string.IsNullOrWhiteSpace(s)));
		var token = CreateToken((int)userId, roleName, trimmedLogin);
		return new AuthLoginResult(true, null, (int)userId, roleName, displayName, token);
	}

	private string CreateToken(int userId, string roleName, string login)
	{
		var claims = new[]
		{
			new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
			new Claim(ClaimTypes.Role, roleName),
			new Claim(ClaimTypes.Name, login)
		};
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
		var token = new JwtSecurityToken(
			issuer: _jwtIssuer,
			audience: _jwtAudience,
			claims: claims,
			expires: DateTime.UtcNow.AddDays(7),
			signingCredentials: creds);
		return new JwtSecurityTokenHandler().WriteToken(token);
	}
}
