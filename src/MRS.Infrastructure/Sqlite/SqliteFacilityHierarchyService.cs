using Microsoft.Data.Sqlite;
using MRS.Application.Facilities;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteFacilityHierarchyService : IFacilityHierarchyService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteFacilityHierarchyService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<IReadOnlyList<HierarchyOption>> GetOrganizationsAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT id, full_name, short_name, legal_form_code
			FROM organizations
			WHERE is_active = 1
			ORDER BY full_name;
			""";
		return await ReadOrganizationOptionsAsync(cmd, cancellationToken).ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<HierarchyOption>> GetFacilitiesAsync(int organizationId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT id, name
			FROM facilities
			WHERE organization_id = $org AND is_active = 1
			ORDER BY name;
			""";
		cmd.Parameters.AddWithValue("$org", organizationId);
		return await ReadOptionsAsync(cmd, cancellationToken).ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<HierarchyOption>> GetFacilitiesWithAddressAsync(int organizationId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT
				f.id,
				a.city,
				a.street,
				a.building,
				a.structure,
				a.block
			FROM facilities f
			INNER JOIN organization_addresses a ON a.id = f.address_id
			WHERE f.organization_id = $org AND f.is_active = 1
			ORDER BY a.city, a.street, a.building, f.id;
			""";
		cmd.Parameters.AddWithValue("$org", organizationId);
		return await ReadFacilityAddressOptionsAsync(cmd, cancellationToken).ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<HierarchyOption>> GetSystemsAsync(int facilityId, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT id, name
			FROM facility_systems
			WHERE facility_id = $fid AND is_active = 1
			ORDER BY name;
			""";
		cmd.Parameters.AddWithValue("$fid", facilityId);
		return await ReadOptionsAsync(cmd, cancellationToken).ConfigureAwait(false);
	}

	private static async Task<IReadOnlyList<HierarchyOption>> ReadOrganizationOptionsAsync(SqliteCommand cmd, CancellationToken cancellationToken)
	{
		var list = new List<HierarchyOption>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var id = reader.GetInt32(0);
			var name = SqliteOrganizationName.ReadListName(reader, 1, 2, 3);
			list.Add(new HierarchyOption(id, name));
		}

		return list;
	}

	private static async Task<IReadOnlyList<HierarchyOption>> ReadFacilityAddressOptionsAsync(SqliteCommand cmd, CancellationToken cancellationToken)
	{
		var list = new List<HierarchyOption>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var id = reader.GetInt32(0);
			var city = reader.GetString(1);
			var street = reader.GetString(2);
			var building = reader.GetString(3);
			var structure = reader.IsDBNull(4) ? null : reader.GetString(4);
			var block = reader.IsDBNull(5) ? null : reader.GetString(5);
			var label = FacilityAddressFormatter.Format(city, street, building, structure, block);
			list.Add(new HierarchyOption(id, label));
		}

		return list;
	}

	private static async Task<IReadOnlyList<HierarchyOption>> ReadOptionsAsync(SqliteCommand cmd, CancellationToken cancellationToken)
	{
		var list = new List<HierarchyOption>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var id = reader.GetInt32(0);
			var name = reader.GetString(1);
			list.Add(new HierarchyOption(id, name));
		}

		return list;
	}
}
