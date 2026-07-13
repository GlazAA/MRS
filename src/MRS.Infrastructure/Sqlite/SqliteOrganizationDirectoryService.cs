using System.Globalization;
using Microsoft.Data.Sqlite;
using MRS.Application.Facilities;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteOrganizationDirectoryService : IOrganizationDirectoryService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteOrganizationDirectoryService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<IReadOnlyList<OrganizationOverviewItem>> ListAsync(
		string? query = null,
		CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);

		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT
				o.id,
				o.full_name,
				o.legal_form_code,
				(SELECT COUNT(1) FROM facilities f WHERE f.organization_id = o.id AND f.is_active = 1),
				(SELECT COUNT(1) FROM organization_employees e WHERE e.organization_id = o.id AND e.is_active = 1)
			FROM organizations o
			WHERE o.is_active = 1
			ORDER BY o.full_name;
			""";

		var tokens = Tokenize(query);
		var phoneDigits = DigitsOnly(query);
		var list = new List<OrganizationOverviewItem>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var id = reader.GetInt32(0);
			var name = reader.GetString(1);
			var formCode = reader.IsDBNull(2) ? null : reader.GetString(2);
			var displayName = FormatOrgName(formCode, name);

			if (tokens.Count > 0 || phoneDigits.Length > 0)
			{
				var matches = await MatchesSearchAsync(connection, id, displayName, name, tokens, phoneDigits, cancellationToken)
					.ConfigureAwait(false);
				if (!matches)
					continue;
			}

			var last = await LoadLastVisitAsync(connection, id, cancellationToken).ConfigureAwait(false);
			list.Add(new OrganizationOverviewItem(
				id,
				displayName,
				formCode,
				LegalFormLabel(formCode),
				reader.GetInt32(3),
				reader.GetInt32(4),
				last.EngineerName,
				last.At));
		}

		return list;
	}

	private static async Task<bool> MatchesSearchAsync(
		SqliteConnection connection,
		int organizationId,
		string displayName,
		string rawName,
		IReadOnlyList<string> tokens,
		string phoneDigits,
		CancellationToken cancellationToken)
	{
		var facilityNames = new List<string>();
		using (var fCmd = connection.CreateCommand())
		{
			fCmd.CommandText = """
				SELECT name FROM facilities
				WHERE organization_id = $org AND is_active = 1;
				""";
			fCmd.Parameters.AddWithValue("$org", organizationId);
			await using var fReader = await fCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			while (await fReader.ReadAsync(cancellationToken).ConfigureAwait(false))
				facilityNames.Add(fReader.GetString(0));
		}

		var contacts = new List<(string Last, string First, string Middle, string Phone)>();
		using (var cCmd = connection.CreateCommand())
		{
			cCmd.CommandText = """
				SELECT last_name, first_name, COALESCE(middle_name, ''), COALESCE(work_phone, ''), COALESCE(work_phone_secondary, '')
				FROM organization_employees
				WHERE organization_id = $org AND is_active = 1;
				""";
			cCmd.Parameters.AddWithValue("$org", organizationId);
			await using var cReader = await cCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			while (await cReader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				contacts.Add((
					cReader.GetString(0),
					cReader.GetString(1),
					cReader.GetString(2),
					$"{cReader.GetString(3)} {cReader.GetString(4)}"));
			}
		}

		if (phoneDigits.Length > 0 &&
		    contacts.Any(c => DigitsOnly(c.Phone).Contains(phoneDigits, StringComparison.Ordinal)))
			return true;

		foreach (var token in tokens)
		{
			if (Contains(displayName, token) || Contains(rawName, token))
				return true;
			if (facilityNames.Any(n => Contains(n, token)))
				return true;
			if (contacts.Any(c =>
				    Contains(c.Last, token) ||
				    Contains(c.First, token) ||
				    Contains(c.Middle, token)))
				return true;

			var tokenDigits = DigitsOnly(token);
			if (tokenDigits.Length > 0 &&
			    contacts.Any(c => DigitsOnly(c.Phone).Contains(tokenDigits, StringComparison.Ordinal)))
				return true;
		}

		return false;
	}

	private static List<string> Tokenize(string? query)
	{
		if (string.IsNullOrWhiteSpace(query))
			return [];

		return query
			.Split([' ', ',', ';', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(t => t.Length > 0)
			.Select(t => t.ToLowerInvariant())
			.Distinct(StringComparer.Ordinal)
			.ToList();
	}

	private static string DigitsOnly(string? value)
	{
		if (string.IsNullOrEmpty(value))
			return string.Empty;
		return new string(value.Where(char.IsDigit).ToArray());
	}

	private static bool Contains(string haystack, string token) =>
		haystack.Contains(token, StringComparison.OrdinalIgnoreCase);

	public async Task<OrganizationDetail?> GetAsync(int organizationId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);

		string name;
		string? formCode;
		using (var orgCmd = connection.CreateCommand())
		{
			orgCmd.CommandText = """
				SELECT full_name, legal_form_code
				FROM organizations
				WHERE id = $id AND is_active = 1;
				""";
			orgCmd.Parameters.AddWithValue("$id", organizationId);
			await using var orgReader = await orgCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			if (!await orgReader.ReadAsync(cancellationToken).ConfigureAwait(false))
				return null;
			name = orgReader.GetString(0);
			formCode = orgReader.IsDBNull(1) ? null : orgReader.GetString(1);
		}

		var facilities = new List<OrganizationFacilityBrief>();
		using (var fCmd = connection.CreateCommand())
		{
			fCmd.CommandText = """
				SELECT f.id, f.name,
				       TRIM(COALESCE(a.city, '') || ', ' || COALESCE(a.street, '') || ', д. ' || COALESCE(a.building, ''))
				FROM facilities f
				LEFT JOIN organization_addresses a ON a.id = f.address_id
				WHERE f.organization_id = $org AND f.is_active = 1
				ORDER BY f.name;
				""";
			fCmd.Parameters.AddWithValue("$org", organizationId);
			await using var fReader = await fCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			while (await fReader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				var address = fReader.IsDBNull(2) ? null : fReader.GetString(2).Trim().Trim(',', ' ');
				facilities.Add(new OrganizationFacilityBrief(
					fReader.GetInt32(0),
					fReader.GetString(1),
					string.IsNullOrWhiteSpace(address) ? null : address));
			}
		}

		var contacts = new List<OrganizationContactBrief>();
		using (var cCmd = connection.CreateCommand())
		{
			cCmd.CommandText = """
				SELECT
					e.id,
					e.facility_id,
					f.name,
					e.last_name,
					e.first_name,
					e.middle_name,
					e.position,
					e.work_phone,
					e.work_email
				FROM organization_employees e
				LEFT JOIN facilities f ON f.id = e.facility_id
				WHERE e.organization_id = $org AND e.is_active = 1
				ORDER BY e.last_name, e.first_name;
				""";
			cCmd.Parameters.AddWithValue("$org", organizationId);
			await using var cReader = await cCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			while (await cReader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				var last = cReader.GetString(3);
				var first = cReader.GetString(4);
				var middle = cReader.IsDBNull(5) ? null : cReader.GetString(5);
				contacts.Add(new OrganizationContactBrief(
					cReader.GetInt32(0),
					cReader.IsDBNull(1) ? null : cReader.GetInt32(1),
					cReader.IsDBNull(2) ? null : cReader.GetString(2),
					last,
					first,
					middle,
					FormatPersonName(last, first, middle),
					cReader.IsDBNull(6) ? null : cReader.GetString(6),
					cReader.IsDBNull(7) ? null : cReader.GetString(7),
					cReader.IsDBNull(8) ? null : cReader.GetString(8)));
			}
		}

		var lastVisit = await LoadLastVisitAsync(connection, organizationId, cancellationToken).ConfigureAwait(false);
		return new OrganizationDetail(
			organizationId,
			FormatOrgName(formCode, name),
			formCode,
			LegalFormLabel(formCode),
			facilities,
			contacts,
			lastVisit.EngineerName,
			lastVisit.At,
			lastVisit.FacilityName);
	}

	private static async Task<(string? EngineerName, DateTimeOffset? At, string? FacilityName)> LoadLastVisitAsync(
		SqliteConnection connection,
		int organizationId,
		CancellationToken cancellationToken)
	{
		(string? Name, DateTimeOffset? At, string? Facility)? best = null;

		using (var visitCmd = connection.CreateCommand())
		{
			visitCmd.CommandText = """
				SELECT
					COALESCE(
						NULLIF(TRIM(COALESCE(u.last_name, '') || ' ' || COALESCE(u.first_name, '') || ' ' || COALESCE(u.middle_name, '')), ''),
						NULLIF(TRIM(COALESCE(ua.last_name, '') || ' ' || COALESCE(ua.first_name, '') || ' ' || COALESCE(ua.middle_name, '')), ''),
						'инженер') AS engineer_name,
					COALESCE(sv.planned_start, sv.created_at) AS visit_at,
					f.name
				FROM scheduled_visits sv
				INNER JOIN facilities f ON f.id = sv.facility_id
				LEFT JOIN scheduled_visit_engineers sve ON sve.scheduled_visit_id = sv.id
				LEFT JOIN users u ON u.id = sve.user_id
				LEFT JOIN users ua ON ua.id = sv.assigned_user_id
				WHERE f.organization_id = $org
				ORDER BY datetime(COALESCE(sv.planned_start, sv.created_at)) DESC
				LIMIT 1;
				""";
			visitCmd.Parameters.AddWithValue("$org", organizationId);
			await using var reader = await visitCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				best = (
					reader.IsDBNull(0) ? null : reader.GetString(0).Trim(),
					ParseDate(reader.IsDBNull(1) ? null : reader.GetString(1)),
					reader.IsDBNull(2) ? null : reader.GetString(2));
			}
		}

		using (var checklistCmd = connection.CreateCommand())
		{
			checklistCmd.CommandText = """
				SELECT
					TRIM(COALESCE(u.last_name, '') || ' ' || COALESCE(u.first_name, '') || ' ' || COALESCE(u.middle_name, '')),
					COALESCE(c.end_at, c.start_at, c.local_updated_at),
					f.name
				FROM checklists c
				INNER JOIN installations i ON i.id = c.installation_id
				INNER JOIN facility_systems fs ON fs.id = i.system_id
				INNER JOIN facilities f ON f.id = fs.facility_id
				INNER JOIN users u ON u.id = c.engineer_id
				WHERE f.organization_id = $org AND c.is_active = 1
				ORDER BY datetime(COALESCE(c.end_at, c.start_at, c.local_updated_at)) DESC
				LIMIT 1;
				""";
			checklistCmd.Parameters.AddWithValue("$org", organizationId);
			await using var reader = await checklistCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				var name = reader.IsDBNull(0) ? null : reader.GetString(0).Trim();
				var at = ParseDate(reader.IsDBNull(1) ? null : reader.GetString(1));
				var facility = reader.IsDBNull(2) ? null : reader.GetString(2);
				if (best is null || (at is not null && (best.Value.At is null || at > best.Value.At)))
					best = (name, at, facility);
			}
		}

		return best is null
			? (null, null, null)
			: (string.IsNullOrWhiteSpace(best.Value.Name) ? null : best.Value.Name, best.Value.At, best.Value.Facility);
	}

	private static DateTimeOffset? ParseDate(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;
		if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
			return dto;
		if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
			return new DateTimeOffset(dt);
		return null;
	}

	private static string FormatOrgName(string? legalFormCode, string companyName)
	{
		var label = LegalFormLabel(legalFormCode);
		if (string.IsNullOrWhiteSpace(label))
			return companyName;
		return $"{label} {companyName}";
	}

	private static string? LegalFormLabel(string? code)
	{
		if (string.IsNullOrWhiteSpace(code))
			return null;
		return OrganizationLegalForm.Choices.FirstOrDefault(c => c.Code == code)?.ShortLabel;
	}

	private static string FormatPersonName(string last, string first, string? middle)
	{
		if (string.IsNullOrWhiteSpace(middle))
			return $"{last} {first}".Trim();
		return $"{last} {first} {middle}".Trim();
	}
}
