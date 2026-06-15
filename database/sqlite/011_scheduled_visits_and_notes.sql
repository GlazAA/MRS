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

-- Контактные лица для демо-организаций
INSERT OR IGNORE INTO organization_employees (id, organization_id, first_name, last_name, middle_name, position, work_phone, work_email, is_active) VALUES
    (1, 1, 'Алексей', 'Петров', 'Иванович', 'Главный инженер', '+7 (495) 111-22-33', 'petrov@mosarchive.demo', 1),
    (2, 1, 'Мария', 'Сидорова', NULL, 'Диспетчер', '4951112234', 'sidorova@mosarchive.demo', 1),
    (3, 2, 'Дмитрий', 'Козлов', 'Сергеевич', 'Начальник участка', '+7-4712-55-66-77', 'kozlov@miratorg.demo', 1),
    (4, 3, 'Елена', 'Волкова', 'Андреевна', 'Ответственная за эксплуатацию', '8 495 999 88 77', 'volkova@sber.demo', 1);

UPDATE facilities SET responsible_employee_id = 1 WHERE id = 1;
UPDATE facilities SET responsible_employee_id = 3 WHERE id = 2;
UPDATE facilities SET responsible_employee_id = 4 WHERE id = 3;

-- Дополнительные инженеры (для будущего мультивыбора)
INSERT OR IGNORE INTO users (id, user_role_id, first_name, last_name, middle_name, login, password_hash, is_active) VALUES
    (3, 1, 'Сергей', 'Николаев', 'Павлович', 'engineer2', '$2a$11$OfflinePlaceholderHashNotForAuth', 1),
    (4, 1, 'Ольга', 'Морозова', 'Викторовна', 'engineer3', '$2a$11$OfflinePlaceholderHashNotForAuth', 1);
