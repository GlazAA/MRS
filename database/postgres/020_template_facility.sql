-- Привязка шаблона КЛ к объекту (facility). NULL = общий/legacy.
ALTER TABLE checklist_templates
    ADD COLUMN IF NOT EXISTS facility_id BIGINT REFERENCES facilities (id);

CREATE INDEX IF NOT EXISTS ix_checklist_templates_facility
    ON checklist_templates (facility_id);

CREATE INDEX IF NOT EXISTS ix_checklist_templates_resolve
    ON checklist_templates (equipment_type_id, maintenance_type_id, facility_id, is_active, version);
