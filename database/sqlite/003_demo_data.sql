-- Демо-данные для оффлайн-разработки (объекты из спецификации: Мосархив/Сахарово, Мираторг/Курск, Сбер/Томилино).
-- Идемпотентно: INSERT OR IGNORE с фиксированными id.

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
    (1, 1, 'Система пожаротушения', 'Демо', 1),
    (2, 2, 'Система пожаротушения', 'Демо', 1),
    (3, 3, 'Система пожаротушения', 'Демо', 1);
