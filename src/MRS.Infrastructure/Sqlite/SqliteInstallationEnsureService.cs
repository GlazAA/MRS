using Microsoft.Data.Sqlite;
using MRS.Application.Facilities;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteInstallationEnsureService : IInstallationEnsureService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteInstallationEnsureService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<int> EnsureAsync(int facilitySystemId, int equipmentTypeId, string unitLabel, CancellationToken cancellationToken = default)
	{
		var label = unitLabel.Trim();
		if (label.Length == 0)
			throw new ArgumentException("Укажите номер или название установки.", nameof(unitLabel));

		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using (var find = connection.CreateCommand())
		{
			find.CommandText = """
				SELECT id FROM installations
				WHERE system_id = $sid AND equipment_type_id = $eid AND is_active = 1
				  AND TRIM(COALESCE(custom_name, '')) = $lbl
				LIMIT 1;
				""";
			find.Parameters.AddWithValue("$sid", facilitySystemId);
			find.Parameters.AddWithValue("$eid", equipmentTypeId);
			find.Parameters.AddWithValue("$lbl", label);
			var existing = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (existing is not null)
				return existing is long l ? (int)l : Convert.ToInt32(existing);
		}

		using var insert = connection.CreateCommand();
		insert.CommandText = """
			INSERT INTO installations (system_id, equipment_type_id, custom_name, is_active)
			VALUES ($sid, $eid, $lbl, 1);
			SELECT last_insert_rowid();
			""";
		insert.Parameters.AddWithValue("$sid", facilitySystemId);
		insert.Parameters.AddWithValue("$eid", equipmentTypeId);
		insert.Parameters.AddWithValue("$lbl", label);
		var scalar = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return scalar is long l2 ? (int)l2 : Convert.ToInt32(scalar ?? throw new InvalidOperationException("Не удалось создать установку."));
	}
}
