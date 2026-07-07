-- Демо-данные для сервера (те же id, что и в SQLite-клиенте).

INSERT INTO ownership_forms (id, code, full_name, short_name) VALUES
    (1, 'OOO', 'Общество с ограниченной ответственностью', 'ООО')
ON CONFLICT (code) DO NOTHING;

INSERT INTO organization_addresses (id, zip_code, country, city, street, building) VALUES
    (1, '125000', 'Россия', 'Москва', 'ул. Демо', '1'),
    (2, '305000', 'Россия', 'Курск', 'ул. Демо', '2'),
    (3, '140070', 'Россия', 'Томилино', 'ул. Демо', '3')
ON CONFLICT (id) DO NOTHING;

SELECT setval(pg_get_serial_sequence('organization_addresses', 'id'), GREATEST((SELECT MAX(id) FROM organization_addresses), 3));

INSERT INTO organizations (id, full_name, short_name, is_active) VALUES
    (1, 'Мосархив', 'Мосархив', TRUE),
    (2, 'Мираторг', 'Мираторг', TRUE),
    (3, 'Сбер', 'Сбер', TRUE)
ON CONFLICT (id) DO UPDATE SET full_name = EXCLUDED.full_name, short_name = EXCLUDED.short_name;

SELECT setval(pg_get_serial_sequence('organizations', 'id'), GREATEST((SELECT MAX(id) FROM organizations), 3));

INSERT INTO organization_data (id, organization_id, legal_address_id, ownership_form_id, inn, ogrn, is_active) VALUES
    (1, 1, 1, 1, '7707083893', '1027700132195', TRUE),
    (2, 2, 2, 1, '4632024910', '1154632000001', TRUE),
    (3, 3, 3, 1, '7707083894', '1027700132196', TRUE)
ON CONFLICT (id) DO NOTHING;

INSERT INTO facilities (id, organization_id, name, address_id, ui_flow, is_active) VALUES
    (1, 1, 'Сахарово', 1, 'object_only', TRUE),
    (2, 2, 'Курск', 2, 'hierarchical', TRUE),
    (3, 3, 'Томилино', 3, 'hierarchical', TRUE)
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, organization_id = EXCLUDED.organization_id;

SELECT setval(pg_get_serial_sequence('facilities', 'id'), GREATEST((SELECT MAX(id) FROM facilities), 3));

INSERT INTO facility_systems (id, facility_id, name, description, is_active) VALUES
    (1, 1, 'Гипаксическая система предотвращения пожара', 'Демо', TRUE),
    (2, 2, 'Гипаксическая система предотвращения пожара', 'Демо', TRUE),
    (3, 3, 'Гипаксическая система предотвращения пожара', 'Демо', TRUE)
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name;

SELECT setval(pg_get_serial_sequence('facility_systems', 'id'), GREATEST((SELECT MAX(id) FROM facility_systems), 3));

INSERT INTO system_equipment_types (system_id, equipment_type_id) VALUES
    (1, 1), (1, 2), (1, 3),
    (2, 1), (2, 2), (2, 4),
    (3, 1), (3, 5), (3, 6)
ON CONFLICT DO NOTHING;

INSERT INTO installations (id, system_id, equipment_type_id, is_active) VALUES
    (1, 1, 1, TRUE),
    (2, 1, 2, TRUE),
    (3, 2, 1, TRUE),
    (4, 3, 1, TRUE)
ON CONFLICT (id) DO UPDATE SET system_id = EXCLUDED.system_id, equipment_type_id = EXCLUDED.equipment_type_id;

SELECT setval(pg_get_serial_sequence('installations', 'id'), GREATEST((SELECT MAX(id) FROM installations), 4));
