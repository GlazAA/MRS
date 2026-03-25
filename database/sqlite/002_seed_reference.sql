-- Справочники для SQLite (идемпотентно через OR IGNORE по UNIQUE).

INSERT OR IGNORE INTO field_types (id, type_name) VALUES
    (1, 'text'),
    (2, 'textarea'),
    (3, 'date'),
    (4, 'time'),
    (5, 'datetime'),
    (6, 'number'),
    (7, 'boolean'),
    (8, 'radio'),
    (9, 'checkbox'),
    (10, 'dropdown'),
    (11, 'dropdown_multiple');

INSERT OR IGNORE INTO validation_rules (id, code, description, error_message) VALUES
    (1, 'no_spaces', 'Нет пробелов в начале/конце', 'Поле не должно содержать пробелы в начале или конце'),
    (2, 'integer', 'Только целые числа', 'Введите целое число'),
    (3, 'positive_number', 'Положительное число', 'Введите положительное число'),
    (4, 'decimal_point', 'Формат 9.0', 'Используйте формат 9.0 (целые и десятые через точку)'),
    (5, 'integer_range_70_100', 'Целое 70–100', 'Значение должно быть в диапазоне 70–100'),
    (6, 'time_format_hhmm', 'ЧЧ:ММ', 'Введите часы и минуты в формате ЧЧ:ММ, например 12:30');

INSERT OR IGNORE INTO maintenance_types (id, type_name, code, description, recommended_interval_days) VALUES
    (1, 'Еженедельное', 'INT-01', NULL, 7),
    (2, 'Ежемесячное', 'INT-02', NULL, 30),
    (3, 'Ежегодное', 'INT-03', NULL, 365),
    (4, '2 года', 'INT-04', NULL, 730),
    (5, '1500', 'INT-05', 'Моточасы / регламент 1500', NULL),
    (6, '3000', 'INT-06', NULL, NULL),
    (7, '6000', 'INT-07', NULL, NULL),
    (8, '9000', 'INT-08', NULL, NULL),
    (9, 'Единое ТО', 'INT-UNIFIED', 'Один шаблон без выбора интервала', NULL);

INSERT OR IGNORE INTO checklist_participant_roles (id, code, display_name) VALUES
    (1, 'responsible', 'Ответственный'),
    (2, 'observer', 'Наблюдатель'),
    (3, 'worker', 'Лицо, производившее работы');

INSERT OR IGNORE INTO equipment_types (id, type_name, code) VALUES
    (1, 'Винтовой компрессор', 'EQ-01'),
    (2, 'Электродвигатель компрессора', 'EQ-02'),
    (3, 'Осушитель холодильного типа', 'EQ-03'),
    (4, 'Циклонный сепаратор', 'EQ-04'),
    (5, 'Фильтры очистки', 'EQ-05'),
    (6, 'Угольный адсорбер', 'EQ-06'),
    (7, 'Конденсатоотводчики', 'EQ-07'),
    (8, 'Водомасляный сепаратор', 'EQ-08'),
    (9, 'Ресиверы', 'EQ-09'),
    (10, 'Газоразделительный модуль', 'EQ-10'),
    (11, 'Центральный шкаф управления', 'EQ-11'),
    (12, 'Шкаф управления зоной защиты', 'EQ-12'),
    (13, 'Датчики, контроллеры и модули', 'EQ-13');

INSERT OR IGNORE INTO user_roles (id, role_name) VALUES
    (1, 'Инженер'),
    (2, 'Администратор'),
    (3, 'Администратор БД');
