-- 013: убрать «Время окончания», дата окончания — необязательна
PRAGMA foreign_keys = ON;

DELETE FROM checklist_responses
WHERE checklist_template_item_id IN (
    SELECT id FROM checklist_template_items WHERE field_code = 'end_time'
);

DELETE FROM checklist_template_items WHERE field_code = 'end_time';

UPDATE checklist_template_items SET is_required = 0 WHERE field_code = 'end_date';
