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
			SELECT id, COALESCE(NULLIF(TRIM(short_name), ''), full_name)
			FROM organizations
			WHERE is_active = 1
			ORDER BY full_name;
			""";
		return await ReadOptionsAsync(cmd, cancellationToken).ConfigureAwait(false);
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
