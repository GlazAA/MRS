using System.Globalization;
using Microsoft.Data.Sqlite;
using MRS.Application.Contacts;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteOrganizationEmployeeQueryService : IOrganizationEmployeeQueryService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteOrganizationEmployeeQueryService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<IReadOnlyList<OrganizationEmployeeOption>> SearchAsync(int organizationId, string? query, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT
				id,
				organization_id,
				first_name,
				last_name,
				COALESCE(middle_name, ''),
				COALESCE(position, ''),
				COALESCE(work_phone, ''),
				COALESCE(work_phone_secondary, ''),
				COALESCE(work_email, '')
			FROM organization_employees
			WHERE organization_id = $org AND is_active = 1
			ORDER BY last_name, first_name;
			""";
		cmd.Parameters.AddWithValue("$org", organizationId);

		var tokens = Tokenize(query);
		var list = new List<OrganizationEmployeeOption>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var id = reader.GetInt32(0);
			var orgId = reader.GetInt32(1);
			var first = reader.GetString(2);
			var last = reader.GetString(3);
			var middle = reader.GetString(4);
			var position = reader.GetString(5);
			var phone1 = reader.GetString(6);
			var phone2 = reader.GetString(7);
			var email = reader.GetString(8);

			var display = FormatPersonName(last, first, middle);
			var haystack = string.Join(' ',
				position, display, phone1, phone2, email, first, last, middle,
				NormalizeDigits(phone1), NormalizeDigits(phone2))
				.ToLowerInvariant();

			if (tokens.Count > 0 && !tokens.All(t => haystack.Contains(t, StringComparison.Ordinal) ||
			                                         NormalizeDigits(t).Length > 0 && haystack.Contains(NormalizeDigits(t), StringComparison.Ordinal)))
				continue;

			list.Add(new OrganizationEmployeeOption(
				id,
				orgId,
				display,
				string.IsNullOrWhiteSpace(position) ? null : position,
				string.IsNullOrWhiteSpace(phone1) ? phone2 : phone1,
				string.IsNullOrWhiteSpace(email) ? null : email));
		}

		return list;
	}

	private static List<string> Tokenize(string? query)
	{
		if (string.IsNullOrWhiteSpace(query))
			return [];

		return query
			.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(t => t.ToLowerInvariant())
			.Where(t => t.Length > 0)
			.Distinct()
			.ToList();
	}

	private static string FormatPersonName(string last, string first, string middle)
	{
		if (string.IsNullOrWhiteSpace(middle))
			return $"{last} {first}".Trim();
		return $"{last} {first} {middle}".Trim();
	}

	private static string NormalizeDigits(string? value)
	{
		if (string.IsNullOrEmpty(value))
			return string.Empty;
		return new string(value.Where(char.IsDigit).ToArray());
	}
}
