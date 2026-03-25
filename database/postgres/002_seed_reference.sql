-- Справочники: типы полей, правила валидации (разд. 2.6 спецификации), виды ТО, роли участников, типы оборудования (разд. 5).
-- Идемпотентно: повторный запуск без дубликатов по уникальным ключам.

INSERT INTO field_types (type_name) VALUES
    ('text'),
    ('textarea'),
    ('date'),
    ('time'),
    ('datetime'),
    ('number'),
    ('boolean'),
    ('radio'),
    ('checkbox'),
    ('dropdown'),
    ('dropdown_multiple')
ON CONFLICT (type_name) DO NOTHING;

INSERT INTO validation_rules (code, description, error_message) VALUES
    ('no_spaces', 'Нет пробелов в начале/конце', 'Поле не должно содержать пробелы в начале или конце'),
    ('integer', 'Только целые числа', 'Введите целое число'),
    ('positive_number', 'Положительное число', 'Введите положительное число'),
    ('decimal_point', 'Формат 9.0', 'Используйте формат 9.0 (целые и десятые через точку)'),
    ('integer_range_70_100', 'Целое 70–100', 'Значение должно быть в диапазоне 70–100'),
    ('time_format_hhmm', 'ЧЧ:ММ', 'Введите часы и минуты в формате ЧЧ:ММ, например 12:30')
ON CONFLICT (code) DO NOTHING;

INSERT INTO maintenance_types (type_name, code, description, recommended_interval_days) VALUES
    ('Еженедельное', 'INT-01', NULL, 7),
    ('Ежемесячное', 'INT-02', NULL, 30),
    ('Ежегодное', 'INT-03', NULL, 365),
    ('2 года', 'INT-04', NULL, 730),
    ('1500', 'INT-05', 'Моточасы / регламент 1500', NULL),
    ('3000', 'INT-06', NULL, NULL),
    ('6000', 'INT-07', NULL, NULL),
    ('9000', 'INT-08', NULL, NULL),
    ('Единое ТО', 'INT-UNIFIED', 'Один шаблон без выбора интервала (см. сценарии 1.9+)', NULL)
ON CONFLICT (code) DO NOTHING;

INSERT INTO checklist_participant_roles (code, display_name) VALUES
    ('responsible', 'Ответственный'),
    ('observer', 'Наблюдатель'),
    ('worker', 'Лицо, производившее работы')
ON CONFLICT (code) DO NOTHING;

INSERT INTO equipment_types (type_name, code) VALUES
    ('Винтовой компрессор', 'EQ-01'),
    ('Электродвигатель компрессора', 'EQ-02'),
    ('Осушитель холодильного типа', 'EQ-03'),
    ('Циклонный сепаратор', 'EQ-04'),
    ('Фильтры очистки', 'EQ-05'),
    ('Угольный адсорбер', 'EQ-06'),
    ('Конденсатоотводчики', 'EQ-07'),
    ('Водомасляный сепаратор', 'EQ-08'),
    ('Ресиверы', 'EQ-09'),
    ('Газоразделительный модуль', 'EQ-10'),
    ('Центральный шкаф управления', 'EQ-11'),
    ('Шкаф управления зоной защиты', 'EQ-12'),
    ('Датчики, контроллеры и модули', 'EQ-13')
ON CONFLICT (type_name) DO NOTHING;

-- Если конфликт по type_name, обновить code отдельным скриптом при необходимости.
INSERT INTO user_roles (role_name) VALUES
    ('Инженер'),
    ('Администратор'),
    ('Администратор БД')
ON CONFLICT (role_name) DO NOTHING;
