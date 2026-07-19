-- Выезды, заметки, контакты (v10)

ALTER TABLE scheduled_visits ADD COLUMN contact_employee_id INTEGER REFERENCES organization_employees (id);
ALTER TABLE scheduled_visits ADD COLUMN contact_manual_text TEXT;
ALTER TABLE scheduled_visits ADD COLUMN prep_skipped INTEGER NOT NULL DEFAULT 0;
ALTER TABLE scheduled_visits ADD COLUMN updated_at TEXT NOT NULL DEFAULT (datetime('now'));

CREATE TABLE IF NOT EXISTS scheduled_visit_engineers (
    scheduled_visit_id INTEGER NOT NULL REFERENCES scheduled_visits (id) ON DELETE CASCADE,
    user_id INTEGER NOT NULL REFERENCES users (id),
    PRIMARY KEY (scheduled_visit_id, user_id)
);

ALTER TABLE engineer_notes ADD COLUMN scheduled_visit_id INTEGER REFERENCES scheduled_visits (id);
ALTER TABLE engineer_notes ADD COLUMN checklist_id INTEGER REFERENCES checklists (id);
ALTER TABLE engineer_notes ADD COLUMN title TEXT;
ALTER TABLE engineer_notes ADD COLUMN completed_at TEXT;

CREATE TABLE IF NOT EXISTS engineer_note_revisions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    engineer_note_id INTEGER NOT NULL REFERENCES engineer_notes (id) ON DELETE CASCADE,
    body TEXT NOT NULL,
    deadline_date TEXT,
    edited_by_user_id INTEGER NOT NULL REFERENCES users (id),
    edited_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS ix_engineer_notes_visit ON engineer_notes (scheduled_visit_id);
CREATE INDEX IF NOT EXISTS ix_engineer_notes_checklist ON engineer_notes (checklist_id);
CREATE INDEX IF NOT EXISTS ix_scheduled_visits_facility ON scheduled_visits (facility_id);
CREATE INDEX IF NOT EXISTS ix_scheduled_visits_start ON scheduled_visits (planned_start);
