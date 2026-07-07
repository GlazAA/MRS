using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteSyncApplyService : ISyncApplyService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteSyncApplyService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task ApplyPullAsync(SyncPullResponse pull, CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
			.ConfigureAwait(false);
		await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		foreach (var org in pull.Organizations)
		{
			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT INTO organizations (id, full_name, short_name, is_active)
				VALUES ($id, $full, $short, $active)
				ON CONFLICT(id) DO UPDATE SET
				    full_name = excluded.full_name,
				    short_name = excluded.short_name,
				    is_active = excluded.is_active;
				""";
			cmd.Parameters.AddWithValue("$id", org.Id);
			cmd.Parameters.AddWithValue("$full", org.FullName);
			cmd.Parameters.AddWithValue("$short", org.ShortName ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("$active", org.IsActive ? 1 : 0);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		foreach (var facility in pull.Facilities)
		{
			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT INTO facilities (id, organization_id, name, address_id, ui_flow, is_active)
				VALUES ($id, $org, $name, COALESCE((SELECT address_id FROM facilities WHERE id = $id), 1), $flow, $active)
				ON CONFLICT(id) DO UPDATE SET
				    organization_id = excluded.organization_id,
				    name = excluded.name,
				    ui_flow = excluded.ui_flow,
				    is_active = excluded.is_active;
				""";
			cmd.Parameters.AddWithValue("$id", facility.Id);
			cmd.Parameters.AddWithValue("$org", facility.OrganizationId);
			cmd.Parameters.AddWithValue("$name", facility.Name);
			cmd.Parameters.AddWithValue("$flow", facility.UiFlow);
			cmd.Parameters.AddWithValue("$active", facility.IsActive ? 1 : 0);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		foreach (var system in pull.FacilitySystems)
		{
			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT INTO facility_systems (id, facility_id, name, description, is_active)
				VALUES ($id, $fid, $name, $desc, $active)
				ON CONFLICT(id) DO UPDATE SET
				    facility_id = excluded.facility_id,
				    name = excluded.name,
				    description = excluded.description,
				    is_active = excluded.is_active;
				""";
			cmd.Parameters.AddWithValue("$id", system.Id);
			cmd.Parameters.AddWithValue("$fid", system.FacilityId);
			cmd.Parameters.AddWithValue("$name", system.Name);
			cmd.Parameters.AddWithValue("$desc", system.Description ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("$active", system.IsActive ? 1 : 0);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		foreach (var inst in pull.Installations)
		{
			using var cmd = connection.CreateCommand();
			cmd.Transaction = tx;
			cmd.CommandText = """
				INSERT INTO installations (id, system_id, equipment_type_id, is_active)
				VALUES ($id, $sid, $eq, $active)
				ON CONFLICT(id) DO UPDATE SET
				    system_id = excluded.system_id,
				    equipment_type_id = excluded.equipment_type_id,
				    is_active = excluded.is_active;
				""";
			cmd.Parameters.AddWithValue("$id", inst.Id);
			cmd.Parameters.AddWithValue("$sid", inst.SystemId);
			cmd.Parameters.AddWithValue("$eq", inst.EquipmentTypeId);
			cmd.Parameters.AddWithValue("$active", inst.IsActive ? 1 : 0);
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
	}
}
