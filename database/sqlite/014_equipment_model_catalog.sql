-- 014: справочник моделей по типу оборудования
PRAGMA foreign_keys = ON;

ALTER TABLE equipment_models ADD COLUMN equipment_type_id INTEGER REFERENCES equipment_types (id);

INSERT INTO equipment_models (equipment_type_id, manufacturer, name) VALUES
    (1, 'Atlas Copco', 'GA'),
    (1, 'Ingersoll Rand', 'R'),
    (1, 'BOGE', 'S-75'),
    (1, 'Kaeser', 'SM 12');
