-- 018: тестовая компания «Демо ТО Комплекс» + заполненные КЛ по шаблонам Mosarchive (для сборки акта).
-- Ответы пишутся через field_code (устойчиво к миграции 015).
PRAGMA foreign_keys = ON;

INSERT OR IGNORE INTO organization_addresses (id, zip_code, country, city, street, building, structure, block) VALUES
    (90, '305025', 'Россия', 'Курск', 'промзона Демо-ТО', '1', NULL, NULL);

INSERT OR IGNORE INTO organizations (id, full_name, short_name, is_active, legal_form_code) VALUES
    (90, 'Демо ТО Комплекс', 'ДемоТО', 1, 'OOO');

INSERT OR IGNORE INTO organization_data (id, organization_id, legal_address_id, ownership_form_id, inn, ogrn, is_active) VALUES
    (90, 90, 90, 1, '4632999901', '1154632999901', 1);

INSERT OR IGNORE INTO facilities (id, organization_id, name, address_id, ui_flow, is_active, contract_address) VALUES
    (90, 90, 'Площадка G301', 90, 'hierarchical', 1, 'Курск, промзона Демо-ТО, 1');

INSERT OR IGNORE INTO facility_systems (id, facility_id, name, description, is_active) VALUES
    (90, 90, 'Гипоксическая система предотвращения пожара', 'Тестовый контур для сборки актов', 1);

INSERT OR IGNORE INTO system_equipment_types (system_id, equipment_type_id)
SELECT 90, et.id FROM equipment_types et;

INSERT OR IGNORE INTO installations (id, system_id, equipment_type_id, custom_name, is_active) VALUES
    (901, 90, 1,  'G301', 1),
    (902, 90, 3,  'G301', 1),
    (903, 90, 5,  'G301', 1),
    (904, 90, 7,  'G301', 1),
    (905, 90, 10, 'G301', 1),
    (906, 90, 4,  'G301', 1),
    (907, 90, 9,  'G301', 1),
    (908, 90, 8,  'G301', 1);

INSERT OR IGNORE INTO checklists (
    id, installation_id, maintenance_type_id, checklist_template_id, engineer_id,
    start_at, end_at, status, is_active, sync_state, client_uuid
) VALUES
    (901, 901, 6, 6,  1, '2025-03-18T08:00:00+03:00', '2025-03-19T17:00:00+03:00', 'completed', 1, 'local', 'demo-act-901'),
    (902, 902, 9, 10, 1, '2025-03-17T09:00:00+03:00', '2025-03-20T16:00:00+03:00', 'completed', 1, 'local', 'demo-act-902'),
    (903, 903, 9, 12, 1, '2025-03-18T10:00:00+03:00', '2025-03-18T12:00:00+03:00', 'completed', 1, 'local', 'demo-act-903'),
    (904, 904, 9, 14, 1, '2025-03-19T11:00:00+03:00', '2025-03-19T13:00:00+03:00', 'completed', 1, 'local', 'demo-act-904'),
    (905, 905, 9, 17, 1, '2025-03-20T09:00:00+03:00', '2025-03-20T11:00:00+03:00', 'completed', 1, 'local', 'demo-act-905'),
    (906, 906, 9, 11, 1, '2025-03-17T14:00:00+03:00', '2025-03-17T15:00:00+03:00', 'completed', 1, 'local', 'demo-act-906'),
    (907, 907, 9, 16, 1, '2025-03-21T10:00:00+03:00', '2025-03-21T11:30:00+03:00', 'completed', 1, 'local', 'demo-act-907'),
    (908, 908, 9, 15, 1, '2025-03-21T12:00:00+03:00', '2025-03-21T13:00:00+03:00', 'completed', 1, 'local', 'demo-act-908'),
    (909, 901, 1, 1,  1, '2025-03-17T08:30:00+03:00', '2025-03-17T10:00:00+03:00', 'completed', 1, 'local', 'demo-act-909');

-- Текстовые ответы по field_code
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 901, id, '2025-03-18' FROM checklist_template_items WHERE checklist_template_id = 6 AND field_code = 'start_date';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 901, id, '08:00' FROM checklist_template_items WHERE checklist_template_id = 6 AND field_code = 'start_time';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 901, id, '7.5' FROM checklist_template_items WHERE checklist_template_id = 6 AND field_code = 'pressure_network';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 901, id, '7.2' FROM checklist_template_items WHERE checklist_template_id = 6 AND field_code = 'pressure_system';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, numeric_response)
SELECT 901, id, 85 FROM checklist_template_items WHERE checklist_template_id = 6 AND field_code = 'final_temp';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 901, id, 'Очистка от пыли узлов компрессора. Замена элемента питания блока управления (CR 2032). Долив масла: 8 л в резервуар и 1 л в масляный фильтр.'
FROM checklist_template_items WHERE checklist_template_id = 6 AND field_code = 'extra_3000';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 901, id, 'Требуется: замена индикатора сервиса регулятора всасывания; замена электромагнитного клапана; установка доп. сепаратора; промывка радиатора комбинированного охладителя.'
FROM checklist_template_items WHERE checklist_template_id = 6 AND field_code = 'remarks_3000';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 901, id, '2025-03-19' FROM checklist_template_items WHERE checklist_template_id = 6 AND field_code = 'end_date';

-- Выбор опций по подписи
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, selected_option_id)
SELECT 901, cti.id, opt.id
FROM checklist_template_items cti
JOIN checklist_template_item_options opt ON opt.checklist_template_item_id = cti.id AND opt.option_label = 'G301'
WHERE cti.checklist_template_id = 6 AND cti.field_code = 'unit_number';

INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, selected_option_id)
SELECT 901, cti.id, opt.id
FROM checklist_template_items cti
JOIN checklist_template_item_options opt ON opt.checklist_template_item_id = cti.id AND opt.option_label = 'Винтовой компрессор'
WHERE cti.checklist_template_id = 6 AND cti.field_code = 'equipment_pick';

INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, selected_option_id)
SELECT 901, cti.id, opt.id
FROM checklist_template_items cti
JOIN checklist_template_item_options opt ON opt.checklist_template_item_id = cti.id AND opt.option_label = 'Под нагрузкой'
WHERE cti.checklist_template_id = 6 AND cti.field_code = 'comp_state';

INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, selected_option_id)
SELECT 901, cti.id, opt.id
FROM checklist_template_items cti
JOIN checklist_template_item_options opt ON opt.checklist_template_item_id = cti.id AND opt.option_label = 'Замена'
WHERE cti.checklist_template_id = 6 AND cti.field_code = 'oil_filter_3000';

INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id)
SELECT 901, id FROM checklist_template_items WHERE checklist_template_id = 6 AND field_code = 'workers';

INSERT OR IGNORE INTO checklist_response_multi_options (checklist_response_id, checklist_template_item_option_id)
SELECT cr.id, opt.id
FROM checklist_responses cr
JOIN checklist_template_items cti ON cti.id = cr.checklist_template_item_id AND cti.field_code = 'workers'
JOIN checklist_template_item_options opt ON opt.checklist_template_item_id = cti.id AND opt.option_label = 'Демо Инженер'
WHERE cr.checklist_id = 901;

-- 909: еженедельное компрессора
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 909, id, '2025-03-17' FROM checklist_template_items WHERE checklist_template_id = 1 AND field_code = 'start_date';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 909, id, '08:30' FROM checklist_template_items WHERE checklist_template_id = 1 AND field_code = 'start_time';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 909, id, 'Визуальный осмотр без замен.' FROM checklist_template_items WHERE checklist_template_id = 1 AND field_code = 'extra_weekly';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 909, id, 'Замечаний по еженедельному ТО нет.' FROM checklist_template_items WHERE checklist_template_id = 1 AND field_code = 'remarks_weekly';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, selected_option_id)
SELECT 909, cti.id, opt.id
FROM checklist_template_items cti
JOIN checklist_template_item_options opt ON opt.checklist_template_item_id = cti.id AND opt.option_label = 'G301'
WHERE cti.checklist_template_id = 1 AND cti.field_code = 'unit_number';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, selected_option_id)
SELECT 909, cti.id, opt.id
FROM checklist_template_items cti
JOIN checklist_template_item_options opt ON opt.checklist_template_item_id = cti.id AND opt.option_label = 'Отсутствует'
WHERE cti.checklist_template_id = 1 AND cti.field_code = 'leak_check_weekly';

-- 902: осушитель
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 902, id, '2025-03-17' FROM checklist_template_items WHERE checklist_template_id = 10 AND field_code = 'start_date';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 902, id, 'Изменение настройки CMD (слив конденсата) с CON на T/N. Замена датчика давления включения вентилятора и датчика температуры точки росы.'
FROM checklist_template_items WHERE checklist_template_id = 10 AND field_code = 'extra_oht';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 902, id, NULL FROM checklist_template_items WHERE checklist_template_id = 10 AND field_code = 'remarks_oht';

-- 903: фильтры
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 903, id, '2025-03-18' FROM checklist_template_items WHERE checklist_template_id = 12 AND field_code = 'start_date';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 903, id, 'Замена фильтров FE65-2P — 1 шт., FE65-2M — 1 шт.'
FROM checklist_template_items WHERE checklist_template_id = 12 AND field_code = 'extra_filters';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 903, id, 'Требуется: устранить негерметичности на трубопроводе (отмечены белым маркером) системы.'
FROM checklist_template_items WHERE checklist_template_id = 12 AND field_code = 'remarks_filters';

-- 904: конденсатоотводчики
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 904, id, '2025-03-19' FROM checklist_template_items WHERE checklist_template_id = 14 AND field_code = 'start_date';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 904, id, 'Замена блоков на конденсатоотводчиках BM32 — 3 шт.'
FROM checklist_template_items WHERE checklist_template_id = 14 AND field_code = 'extra_cond';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 904, id, 'Требуется замена блоков BM32 — 3 шт. (заказ расходников).'
FROM checklist_template_items WHERE checklist_template_id = 14 AND field_code = 'remarks_cond';

-- 905: ГРМ
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 905, id, '2025-03-20' FROM checklist_template_items WHERE checklist_template_id = 17 AND field_code = 'start_date';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 905, id, 'Проверка заданных состояний и предельных значений.'
FROM checklist_template_items WHERE checklist_template_id = 17 AND field_code = 'extra_grm';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 905, id, 'Заменить датчики контроля на газогенераторе PL210M.'
FROM checklist_template_items WHERE checklist_template_id = 17 AND field_code = 'remarks_grm';

-- 906: циклон
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 906, id, '2025-03-17' FROM checklist_template_items WHERE checklist_template_id = 11 AND field_code = 'start_date';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 906, id, 'Очистка циклонного сепаратора.'
FROM checklist_template_items WHERE checklist_template_id = 11 AND field_code = 'extra_cyclone';

-- 907: ресивер
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 907, id, '2025-03-21' FROM checklist_template_items WHERE checklist_template_id = 16 AND field_code = 'start_date';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 907, id, 'Проверка ресивера воздуха/N2.'
FROM checklist_template_items WHERE checklist_template_id = 16 AND field_code = 'extra_recv';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 907, id, 'Требуется замена манометра на ресивере сжатого воздуха MB 811 200 411.'
FROM checklist_template_items WHERE checklist_template_id = 16 AND field_code = 'remarks_recv';

-- 908: ВМС
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 908, id, '2025-03-21' FROM checklist_template_items WHERE checklist_template_id = 15 AND field_code = 'start_date';
INSERT OR IGNORE INTO checklist_responses (checklist_id, checklist_template_item_id, text_response)
SELECT 908, id, 'Замена фильтров по индикаторам ВМС.'
FROM checklist_template_items WHERE checklist_template_id = 15 AND field_code = 'extra_wms';
