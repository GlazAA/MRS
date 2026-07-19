-- 022: чистый старт для клиента — убрать демо/операционные данные.
-- Оставляем: справочники (в т.ч. виды ТО / типы оборудования), шаблоны КЛ, каталог моделей, формы собственности.
-- Удаляем: организации, объекты, установки, КЛ, выезды, заметки, контакты клиентов, всех сотрудников (users).

PRAGMA foreign_keys = ON;

DELETE FROM checklist_response_multi_options;
DELETE FROM checklist_responses;
DELETE FROM checklist_participants;
DELETE FROM checklist_documentation;
DELETE FROM media_files;
DELETE FROM maintenance_history;
DELETE FROM checklists;

DELETE FROM engineer_note_revisions;
DELETE FROM engineer_notes;
DELETE FROM visit_consumables;
DELETE FROM scheduled_visit_engineers;
DELETE FROM scheduled_visits;

DELETE FROM admin_support_requests;
DELETE FROM sync_entity_locks;
DELETE FROM sync_outbox;
DELETE FROM user_refresh_tokens;
DELETE FROM user_personal_data;

DELETE FROM installations;
DELETE FROM system_equipment_types;
DELETE FROM facility_systems;

UPDATE facilities SET responsible_employee_id = NULL, secondary_contact_id = NULL;
DELETE FROM organization_employees;
DELETE FROM facilities;
DELETE FROM organization_data;
DELETE FROM organization_history;
DELETE FROM organizations;
DELETE FROM organization_addresses;
DELETE FROM banks;

DELETE FROM users;
