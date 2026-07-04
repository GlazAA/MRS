-- 015: производитель/модель, удаление устаревших полей, очистка тестовых листов
PRAGMA foreign_keys = ON;

DELETE FROM checklist_response_multi_options;
DELETE FROM checklist_responses;
DELETE FROM checklists;

DELETE FROM checklist_template_item_options WHERE option_label = 'Другое';

DELETE FROM checklist_template_items
WHERE field_code IN ('comp_type', 'operating_hours', 'motor_hours_note');

UPDATE checklist_template_items SET hint_text = NULL WHERE field_code = 'final_temp';

DELETE FROM checklist_template_item_options
WHERE checklist_template_item_id IN (
    SELECT id FROM checklist_template_items WHERE field_code = 'comp_model'
);

-- Компрессоры 1–8: comp_model -> comp_manufacturer, добавить comp_model
UPDATE checklist_template_items
SET field_code = 'comp_manufacturer', question_text = 'Производитель компрессора', field_type_id = 10
WHERE field_code = 'comp_model' AND checklist_template_id BETWEEN 1 AND 8;

UPDATE checklist_template_items SET sort_order = sort_order + 1
WHERE checklist_template_id BETWEEN 1 AND 8 AND sort_order >= 7;

INSERT INTO checklist_template_items (checklist_template_id, sort_order, field_code, question_text, hint_text, field_type_id, is_required, validation_rule_code)
SELECT checklist_template_id, 7, 'comp_model', 'Модель компрессора', NULL, 10, 0, NULL
FROM checklist_template_items
WHERE field_code = 'comp_manufacturer' AND checklist_template_id BETWEEN 1 AND 8;

-- Единое ТО: *_model -> *_manufacturer + *_model (dropdown)
UPDATE checklist_template_items SET field_code = 'motor_manufacturer', question_text = 'Производитель ПЭД', field_type_id = 10
WHERE checklist_template_id = 9 AND field_code = 'motor_model';
INSERT INTO checklist_template_items (checklist_template_id, sort_order, field_code, question_text, field_type_id, is_required)
VALUES (9, 6, 'motor_model', 'Модель ПЭД', 10, 1);

UPDATE checklist_template_items SET sort_order = sort_order + 1 WHERE checklist_template_id = 10 AND sort_order >= 6;
UPDATE checklist_template_items SET field_code = 'oht_manufacturer', question_text = 'Производитель ОХТ', field_type_id = 10
WHERE checklist_template_id = 10 AND field_code = 'oht_model';
INSERT INTO checklist_template_items (checklist_template_id, sort_order, field_code, question_text, field_type_id, is_required)
VALUES (10, 6, 'oht_model', 'Модель ОХТ', 10, 1);

UPDATE checklist_template_items SET sort_order = sort_order + 1 WHERE checklist_template_id = 11 AND sort_order >= 6;
UPDATE checklist_template_items SET field_code = 'cyclone_manufacturer', question_text = 'Производитель ЦС', field_type_id = 10
WHERE checklist_template_id = 11 AND field_code = 'cyclone_model';
INSERT INTO checklist_template_items (checklist_template_id, sort_order, field_code, question_text, field_type_id, is_required)
VALUES (11, 6, 'cyclone_model', 'Модель ЦС', 10, 1);

UPDATE checklist_template_items SET sort_order = sort_order + 1 WHERE checklist_template_id = 12 AND sort_order >= 6;
UPDATE checklist_template_items SET field_code = 'filter_manufacturer', question_text = 'Производитель фильтра', field_type_id = 10
WHERE checklist_template_id = 12 AND field_code = 'filter_model';
INSERT INTO checklist_template_items (checklist_template_id, sort_order, field_code, question_text, field_type_id, is_required)
VALUES (12, 6, 'filter_model', 'Модель фильтра', 10, 1);

UPDATE checklist_template_items SET sort_order = sort_order + 1 WHERE checklist_template_id = 13 AND sort_order >= 6;
UPDATE checklist_template_items SET field_code = 'ads_manufacturer', question_text = 'Производитель адсорбера', field_type_id = 10
WHERE checklist_template_id = 13 AND field_code = 'ads_model';
INSERT INTO checklist_template_items (checklist_template_id, sort_order, field_code, question_text, field_type_id, is_required)
VALUES (13, 6, 'ads_model', 'Модель адсорбера', 10, 1);

UPDATE checklist_template_items SET sort_order = sort_order + 1 WHERE checklist_template_id = 14 AND sort_order >= 6;
UPDATE checklist_template_items SET field_code = 'cond_manufacturer', question_text = 'Производитель КО', field_type_id = 10
WHERE checklist_template_id = 14 AND field_code = 'cond_model';
INSERT INTO checklist_template_items (checklist_template_id, sort_order, field_code, question_text, field_type_id, is_required)
VALUES (14, 6, 'cond_model', 'Модель КО', 10, 1);

UPDATE checklist_template_items SET sort_order = sort_order + 1 WHERE checklist_template_id = 15 AND sort_order >= 6;
UPDATE checklist_template_items SET field_code = 'wms_manufacturer', question_text = 'Производитель ВМС', field_type_id = 10
WHERE checklist_template_id = 15 AND field_code = 'wms_model';
INSERT INTO checklist_template_items (checklist_template_id, sort_order, field_code, question_text, field_type_id, is_required)
VALUES (15, 6, 'wms_model', 'Модель ВМС', 10, 1);

UPDATE checklist_template_items SET sort_order = sort_order + 1 WHERE checklist_template_id = 16 AND sort_order >= 6;
UPDATE checklist_template_items SET field_code = 'recv_manufacturer', question_text = 'Производитель ресивера', field_type_id = 10
WHERE checklist_template_id = 16 AND field_code = 'recv_model';
INSERT INTO checklist_template_items (checklist_template_id, sort_order, field_code, question_text, field_type_id, is_required)
VALUES (16, 6, 'recv_model', 'Модель ресивера', 10, 1);

UPDATE checklist_template_items SET sort_order = sort_order + 1 WHERE checklist_template_id = 17 AND sort_order >= 6;
UPDATE checklist_template_items SET field_code = 'grm_manufacturer', question_text = 'Производитель ГРМ', field_type_id = 10
WHERE checklist_template_id = 17 AND field_code = 'grm_model';
INSERT INTO checklist_template_items (checklist_template_id, sort_order, field_code, question_text, field_type_id, is_required)
VALUES (17, 6, 'grm_model', 'Модель ГРМ', 10, 1);

UPDATE checklist_template_items SET sort_order = sort_order + 1 WHERE checklist_template_id = 18 AND sort_order >= 8;
UPDATE checklist_template_items SET field_code = 'cshu_battery_manufacturer', question_text = 'Производитель АКБ ЦШУ', field_type_id = 10
WHERE checklist_template_id = 18 AND field_code = 'cshu_battery_model';
INSERT INTO checklist_template_items (checklist_template_id, sort_order, field_code, question_text, field_type_id, is_required)
VALUES (18, 8, 'cshu_battery_model', 'Модель АКБ ЦШУ', 10, 0);

UPDATE checklist_template_items SET sort_order = sort_order + 1 WHERE checklist_template_id = 19 AND sort_order >= 8;
UPDATE checklist_template_items SET field_code = 'shuzz_battery_manufacturer', question_text = 'Производитель АКБ ШУЗЗ', field_type_id = 10
WHERE checklist_template_id = 19 AND field_code = 'shuzz_battery_model';
INSERT INTO checklist_template_items (checklist_template_id, sort_order, field_code, question_text, field_type_id, is_required)
VALUES (19, 8, 'shuzz_battery_model', 'Модель АКБ ШУЗЗ', 10, 0);

UPDATE checklist_template_items SET sort_order = sort_order + 1 WHERE checklist_template_id = 20 AND sort_order >= 6;
UPDATE checklist_template_items SET field_code = 'dcm_manufacturer', question_text = 'Производитель устройства', field_type_id = 10
WHERE checklist_template_id = 20 AND field_code = 'dcm_model';
INSERT INTO checklist_template_items (checklist_template_id, sort_order, field_code, question_text, field_type_id, is_required)
VALUES (20, 6, 'dcm_model', 'Модель устройства', 10, 1);
