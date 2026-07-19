using Microsoft.Data.Sqlite;
using MRS.Application.Storage;
using MRS.Infrastructure.Sqlite;

namespace MRS.Infrastructure.Tests;

/// <summary>Минимальный демо-граф организаций/установок для тестов (после очистки клиентских данных).</summary>
internal static class TestDemoOperationalSeed
{
	public static async Task EnsureAsync(string databasePath, ILocalDatabaseBootstrapper bootstrapper)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(
			new FixedPath(databasePath), bootstrapper, CancellationToken.None).ConfigureAwait(false);

		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			INSERT OR IGNORE INTO ownership_forms (id, code, full_name, short_name) VALUES
			    (1, 'OOO', 'Общество с ограниченной ответственностью', 'ООО');

			INSERT OR IGNORE INTO organization_addresses (id, zip_code, country, city, street, building) VALUES
			    (1, '125000', 'Россия', 'Москва', 'ул. Демо', '1'),
			    (2, '305000', 'Россия', 'Курск', 'ул. Демо', '2'),
			    (3, '140070', 'Россия', 'Томилино', 'ул. Демо', '3');

			INSERT OR IGNORE INTO organizations (id, full_name, short_name, is_active) VALUES
			    (1, 'Мосархив', 'Мосархив', 1),
			    (2, 'Мираторг', 'Мираторг', 1),
			    (3, 'Сбер', 'Сбер', 1);

			INSERT OR IGNORE INTO organization_data (id, organization_id, legal_address_id, ownership_form_id, inn, ogrn, is_active) VALUES
			    (1, 1, 1, 1, '7707083893', '1027700132195', 1),
			    (2, 2, 2, 1, '4632024910', '1154632000001', 1),
			    (3, 3, 3, 1, '7707083894', '1027700132196', 1);

			INSERT OR IGNORE INTO facilities (id, organization_id, name, address_id, ui_flow, is_active) VALUES
			    (1, 1, 'Сахарово', 1, 'object_only', 1),
			    (2, 2, 'Курск', 2, 'hierarchical', 1),
			    (3, 3, 'Томилино', 3, 'hierarchical', 1);

			INSERT OR IGNORE INTO facility_systems (id, facility_id, name, description, is_active) VALUES
			    (1, 1, 'Гипоксическая система предотвращения пожара', 'Демо', 1),
			    (2, 2, 'Гипоксическая система предотвращения пожара', 'Демо', 1),
			    (3, 3, 'Гипоксическая система предотвращения пожара', 'Демо', 1);

			INSERT OR IGNORE INTO system_equipment_types (system_id, equipment_type_id)
			SELECT s.n, et.id
			FROM equipment_types et
			CROSS JOIN (SELECT 1 AS n UNION ALL SELECT 2 UNION ALL SELECT 3) s;

			INSERT OR IGNORE INTO installations (id, system_id, equipment_type_id, is_active) VALUES
			    (1, 1, 1, 1),
			    (2, 1, 2, 1),
			    (3, 2, 1, 1),
			    (4, 3, 1, 1);

			INSERT OR IGNORE INTO users (id, user_role_id, first_name, last_name, login, password_hash, is_active) VALUES
			    (1, 1, 'Полевой', 'Инженер', 'engineer', '$2a$11$OfflinePlaceholderHashNotForAuth', 1),
			    (3, 1, 'Сергей', 'Николаев', 'engineer2', '$2a$11$OfflinePlaceholderHashNotForAuth', 1),
			    (4, 1, 'Ольга', 'Морозова', 'engineer3', '$2a$11$OfflinePlaceholderHashNotForAuth', 1);

			INSERT OR IGNORE INTO organization_employees (id, organization_id, first_name, last_name, middle_name, position, work_phone, work_email, is_active, facility_id) VALUES
			    (1, 1, 'Алексей', 'Петров', 'Иванович', 'Главный инженер', '+7 (495) 111-22-33', 'petrov@mosarchive.demo', 1, 1),
			    (2, 1, 'Мария', 'Сидорова', NULL, 'Диспетчер', '4951112234', 'sidorova@mosarchive.demo', 1, 1),
			    (3, 2, 'Дмитрий', 'Козлов', 'Сергеевич', 'Начальник участка', '+7-4712-55-66-77', 'kozlov@miratorg.demo', 1, 2),
			    (4, 3, 'Елена', 'Волкова', 'Андреевна', 'Ответственная за эксплуатацию', '8 495 999 88 77', 'volkova@sber.demo', 1, 3);

			UPDATE facilities SET responsible_employee_id = 1 WHERE id = 1;
			UPDATE facilities SET responsible_employee_id = 3 WHERE id = 2;
			UPDATE facilities SET responsible_employee_id = 4 WHERE id = 3;
			""";
		await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private sealed class FixedPath(string path) : ILocalDatabasePath
	{
		public string GetDatabaseFilePath() => path;
	}
}
