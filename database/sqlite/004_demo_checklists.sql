-- Демо: связь систем с типами оборудования, пользователь-инженер, установки, контрольные листы для информационного окна.

INSERT OR IGNORE INTO users (id, user_role_id, first_name, last_name, login, password_hash, is_active) VALUES
    (1, 1, 'Демо', 'Инженер', 'demo', '$2a$11$OfflinePlaceholderHashNotForAuth', 1);

INSERT OR IGNORE INTO system_equipment_types (system_id, equipment_type_id) VALUES
    (1, 1), (1, 2), (1, 3),
    (2, 1), (2, 2), (2, 4),
    (3, 1), (3, 5), (3, 6);

INSERT OR IGNORE INTO installations (id, system_id, equipment_type_id, is_active) VALUES
    (1, 1, 1, 1),
    (2, 1, 2, 1),
    (3, 2, 1, 1),
    (4, 3, 1, 1);

INSERT OR IGNORE INTO checklists (id, installation_id, maintenance_type_id, engineer_id, start_at, end_at, status, is_active, sync_state) VALUES
    (1, 1, 1, 1, '2025-12-22T09:30:00Z', '2025-12-22T11:00:00Z', 'completed', 1, 'local'),
    (2, 2, 2, 1, '2025-12-23T08:15:00Z', NULL, 'in_progress', 1, 'local'),
    (3, 1, 1, 1, '2025-12-20T14:00:00Z', NULL, 'draft', 1, 'local');
