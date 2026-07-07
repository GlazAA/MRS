-- Дополнительные объекты схемы (синхронизация с SQLite v14).

CREATE TABLE IF NOT EXISTS admin_support_requests (
    id BIGSERIAL PRIMARY KEY,
    author_user_id BIGINT REFERENCES users (id),
    author_display_name TEXT NOT NULL,
    body TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'open' CHECK (status IN ('open', 'resolved')),
    admin_reply TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    resolved_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_admin_support_requests_status ON admin_support_requests (status);

ALTER TABLE organizations ADD COLUMN IF NOT EXISTS legal_form_code TEXT;
ALTER TABLE organization_addresses ADD COLUMN IF NOT EXISTS structure TEXT;
ALTER TABLE facilities ADD COLUMN IF NOT EXISTS contract_address TEXT;

ALTER TABLE scheduled_visits ADD COLUMN IF NOT EXISTS contact_employee_id BIGINT REFERENCES organization_employees (id);
ALTER TABLE scheduled_visits ADD COLUMN IF NOT EXISTS contact_manual_text TEXT;
ALTER TABLE scheduled_visits ADD COLUMN IF NOT EXISTS prep_skipped BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE scheduled_visits ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW();

CREATE TABLE IF NOT EXISTS scheduled_visit_engineers (
    scheduled_visit_id BIGINT NOT NULL REFERENCES scheduled_visits (id) ON DELETE CASCADE,
    user_id BIGINT NOT NULL REFERENCES users (id),
    PRIMARY KEY (scheduled_visit_id, user_id)
);

ALTER TABLE engineer_notes ADD COLUMN IF NOT EXISTS scheduled_visit_id BIGINT REFERENCES scheduled_visits (id);
ALTER TABLE engineer_notes ADD COLUMN IF NOT EXISTS checklist_id BIGINT REFERENCES checklists (id);
ALTER TABLE engineer_notes ADD COLUMN IF NOT EXISTS title TEXT;
ALTER TABLE engineer_notes ADD COLUMN IF NOT EXISTS completed_at TIMESTAMPTZ;

CREATE TABLE IF NOT EXISTS engineer_note_revisions (
    id BIGSERIAL PRIMARY KEY,
    engineer_note_id BIGINT NOT NULL REFERENCES engineer_notes (id) ON DELETE CASCADE,
    body TEXT NOT NULL,
    deadline_date DATE,
    edited_by_user_id BIGINT NOT NULL REFERENCES users (id),
    edited_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE equipment_models ADD COLUMN IF NOT EXISTS equipment_type_id BIGINT REFERENCES equipment_types (id);

CREATE TABLE IF NOT EXISTS schema_migrations (
    version INT PRIMARY KEY,
    applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
