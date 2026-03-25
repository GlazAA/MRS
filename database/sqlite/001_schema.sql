-- MRS: схема SQLite для локального оффлайн-хранилища (MAUI)
-- Соглашения: INTEGER 0/1 вместо BOOLEAN; моменты времени — TEXT ISO8601; client_uuid — TEXT (UUID).
--
-- Шаблоны «без вида ТО» в интерфейсе: maintenance_types.code = 'INT-UNIFIED'.
-- facilities.ui_flow: object_only | hierarchical. Комбобокс: option_id или text_response.

PRAGMA foreign_keys = ON;

-- ---------------------------------------------------------------------------
-- 1. Пользователи, роли, безопасность
-- ---------------------------------------------------------------------------

CREATE TABLE user_roles (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    role_name TEXT NOT NULL UNIQUE
);

CREATE TABLE users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_role_id INTEGER NOT NULL REFERENCES user_roles (id),
    first_name TEXT,
    last_name TEXT,
    middle_name TEXT,
    login TEXT NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    email TEXT,
    phone_number TEXT,
    phone_number_secondary TEXT,
    is_active INTEGER NOT NULL DEFAULT 1,
    failed_login_count INTEGER NOT NULL DEFAULT 0,
    locked_until TEXT,
    password_changed_at TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE user_personal_data (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL UNIQUE REFERENCES users (id) ON DELETE CASCADE,
    passport_series TEXT,
    passport_number TEXT,
    passport_issued_by TEXT,
    passport_issue_date TEXT,
    passport_department_code TEXT
);

CREATE TABLE user_refresh_tokens (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    token_hash TEXT NOT NULL,
    expires_at TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    revoked_at TEXT,
    device_id TEXT,
    user_agent TEXT
);

CREATE INDEX ix_user_refresh_tokens_user_id ON user_refresh_tokens (user_id);

-- ---------------------------------------------------------------------------
-- 2. Организации и сотрудники заказчика
-- ---------------------------------------------------------------------------

CREATE TABLE banks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    bic TEXT,
    correspondent_account TEXT
);

CREATE TABLE organization_addresses (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    zip_code TEXT,
    country TEXT NOT NULL DEFAULT 'Россия',
    region TEXT,
    city TEXT NOT NULL,
    street TEXT NOT NULL,
    building TEXT NOT NULL,
    block TEXT,
    entrance TEXT,
    apartment_office TEXT
);

CREATE TABLE ownership_forms (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    code TEXT NOT NULL UNIQUE,
    full_name TEXT NOT NULL,
    short_name TEXT NOT NULL
);

CREATE TABLE organizations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    full_name TEXT NOT NULL,
    short_name TEXT,
    is_active INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE organization_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    organization_id INTEGER NOT NULL REFERENCES organizations (id) ON DELETE CASCADE,
    full_name TEXT NOT NULL,
    change_date TEXT NOT NULL,
    change_reason TEXT
);

CREATE TABLE organization_data (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    organization_id INTEGER NOT NULL REFERENCES organizations (id) ON DELETE CASCADE,
    legal_address_id INTEGER NOT NULL REFERENCES organization_addresses (id),
    ownership_form_id INTEGER NOT NULL REFERENCES ownership_forms (id),
    inn TEXT NOT NULL CHECK (length(inn) IN (10, 12) AND inn GLOB '[0-9]*'),
    kpp TEXT CHECK (kpp IS NULL OR (length(kpp) = 9 AND kpp GLOB '[0-9]*')),
    ogrn TEXT NOT NULL CHECK (
        (length(ogrn) = 13 AND ogrn GLOB '[0-9]*') OR
        (length(ogrn) = 15 AND ogrn GLOB '[0-9]*')
    ),
    bank_id INTEGER REFERENCES banks (id),
    payment_account TEXT,
    ceo_full_name TEXT,
    is_active INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE organization_employees (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    organization_id INTEGER NOT NULL REFERENCES organizations (id),
    first_name TEXT NOT NULL,
    last_name TEXT NOT NULL,
    middle_name TEXT,
    position TEXT,
    work_phone TEXT,
    work_phone_secondary TEXT,
    work_email TEXT,
    is_active INTEGER NOT NULL DEFAULT 1
);

-- ---------------------------------------------------------------------------
-- 3. Объекты, системы, установки
-- ---------------------------------------------------------------------------

CREATE TABLE facilities (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    organization_id INTEGER NOT NULL REFERENCES organizations (id),
    responsible_employee_id INTEGER REFERENCES organization_employees (id),
    secondary_contact_id INTEGER REFERENCES organization_employees (id),
    name TEXT NOT NULL,
    address_id INTEGER NOT NULL REFERENCES organization_addresses (id),
    ui_flow TEXT NOT NULL DEFAULT 'hierarchical' CHECK (ui_flow IN ('hierarchical', 'object_only')),
    is_active INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE facility_systems (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    facility_id INTEGER NOT NULL REFERENCES facilities (id),
    name TEXT NOT NULL,
    description TEXT,
    is_active INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE equipment_models (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    manufacturer TEXT,
    specifications TEXT
);

CREATE TABLE equipment_types (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    type_name TEXT NOT NULL UNIQUE,
    code TEXT UNIQUE
);

CREATE TABLE system_equipment_types (
    system_id INTEGER NOT NULL REFERENCES facility_systems (id) ON DELETE CASCADE,
    equipment_type_id INTEGER NOT NULL REFERENCES equipment_types (id) ON DELETE CASCADE,
    PRIMARY KEY (system_id, equipment_type_id)
);

CREATE TABLE installations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    system_id INTEGER NOT NULL REFERENCES facility_systems (id),
    equipment_type_id INTEGER NOT NULL REFERENCES equipment_types (id),
    equipment_model_id INTEGER REFERENCES equipment_models (id),
    custom_name TEXT,
    standard_model_name TEXT,
    standard_serial_number TEXT,
    custom_model_name TEXT,
    custom_serial_number TEXT,
    is_data_modified INTEGER NOT NULL DEFAULT 0,
    is_active INTEGER NOT NULL DEFAULT 1
);

-- ---------------------------------------------------------------------------
-- 4. Типы ТО, поля, валидация, шаблоны
-- ---------------------------------------------------------------------------

CREATE TABLE maintenance_types (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    type_name TEXT NOT NULL,
    code TEXT UNIQUE,
    description TEXT,
    recommended_interval_days INTEGER
);

CREATE TABLE field_types (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    type_name TEXT NOT NULL UNIQUE
);

CREATE TABLE validation_rules (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    code TEXT NOT NULL UNIQUE,
    description TEXT,
    error_message TEXT
);

CREATE TABLE checklist_templates (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    equipment_type_id INTEGER NOT NULL REFERENCES equipment_types (id),
    maintenance_type_id INTEGER NOT NULL REFERENCES maintenance_types (id),
    template_name TEXT NOT NULL,
    scenario_code TEXT UNIQUE,
    version INTEGER NOT NULL DEFAULT 1,
    is_active INTEGER NOT NULL DEFAULT 1,
    top_plate_text TEXT,
    safety_modal_text TEXT,
    red_button_enabled INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE checklist_template_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    checklist_template_id INTEGER NOT NULL REFERENCES checklist_templates (id) ON DELETE CASCADE,
    sort_order INTEGER NOT NULL,
    field_code TEXT,
    question_text TEXT NOT NULL,
    hint_text TEXT,
    field_type_id INTEGER NOT NULL REFERENCES field_types (id),
    validation_rule_code TEXT REFERENCES validation_rules (code),
    is_required INTEGER NOT NULL DEFAULT 0,
    visible_when_maintenance_type_code TEXT,
    group_name TEXT
);

CREATE INDEX ix_checklist_template_items_template ON checklist_template_items (checklist_template_id);

CREATE TABLE checklist_template_item_options (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    checklist_template_item_id INTEGER NOT NULL REFERENCES checklist_template_items (id) ON DELETE CASCADE,
    sort_order INTEGER NOT NULL,
    option_code TEXT,
    option_label TEXT NOT NULL
);

CREATE INDEX ix_template_item_options_item ON checklist_template_item_options (checklist_template_item_id);

-- ---------------------------------------------------------------------------
-- 5. Контрольные листы и ответы
-- ---------------------------------------------------------------------------

CREATE TABLE checklist_participant_roles (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    code TEXT NOT NULL UNIQUE,
    display_name TEXT NOT NULL
);

CREATE TABLE checklists (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    installation_id INTEGER NOT NULL REFERENCES installations (id),
    maintenance_type_id INTEGER NOT NULL REFERENCES maintenance_types (id),
    checklist_template_id INTEGER REFERENCES checklist_templates (id),
    engineer_id INTEGER NOT NULL REFERENCES users (id),
    responsible_employee_id INTEGER REFERENCES organization_employees (id),
    start_at TEXT,
    end_at TEXT,
    status TEXT NOT NULL DEFAULT 'draft' CHECK (status IN ('draft', 'in_progress', 'completed', 'cancelled')),
    is_active INTEGER NOT NULL DEFAULT 1,
    client_uuid TEXT UNIQUE,
    sync_state TEXT NOT NULL DEFAULT 'local' CHECK (sync_state IN ('local', 'pending_upload', 'synced', 'conflict')),
    server_updated_at TEXT,
    local_updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX ix_checklists_installation ON checklists (installation_id);
CREATE INDEX ix_checklists_engineer ON checklists (engineer_id);
CREATE INDEX ix_checklists_sync ON checklists (sync_state);

CREATE TABLE checklist_participants (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    checklist_id INTEGER NOT NULL REFERENCES checklists (id) ON DELETE CASCADE,
    role_id INTEGER NOT NULL REFERENCES checklist_participant_roles (id),
    user_id INTEGER REFERENCES users (id),
    organization_employee_id INTEGER REFERENCES organization_employees (id),
    display_name_snapshot TEXT,
    sort_order INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX ix_checklist_participants_checklist ON checklist_participants (checklist_id);

CREATE TABLE checklist_responses (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    checklist_id INTEGER NOT NULL REFERENCES checklists (id) ON DELETE CASCADE,
    checklist_template_item_id INTEGER NOT NULL REFERENCES checklist_template_items (id),
    boolean_response INTEGER,
    text_response TEXT,
    numeric_response REAL,
    selected_option_id INTEGER REFERENCES checklist_template_item_options (id),
    UNIQUE (checklist_id, checklist_template_item_id)
);

CREATE INDEX ix_checklist_responses_checklist ON checklist_responses (checklist_id);

CREATE TABLE checklist_response_multi_options (
    checklist_response_id INTEGER NOT NULL REFERENCES checklist_responses (id) ON DELETE CASCADE,
    checklist_template_item_option_id INTEGER NOT NULL REFERENCES checklist_template_item_options (id) ON DELETE CASCADE,
    PRIMARY KEY (checklist_response_id, checklist_template_item_option_id)
);

CREATE TABLE maintenance_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    installation_id INTEGER NOT NULL REFERENCES installations (id),
    checklist_id INTEGER REFERENCES checklists (id),
    maintenance_type_id INTEGER NOT NULL REFERENCES maintenance_types (id),
    maintenance_date TEXT NOT NULL,
    next_maintenance_date TEXT,
    engineer_id INTEGER REFERENCES users (id),
    comments TEXT,
    is_active INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE media_files (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    checklist_id INTEGER NOT NULL REFERENCES checklists (id) ON DELETE CASCADE,
    file_path TEXT NOT NULL,
    file_name TEXT,
    mime_type TEXT,
    description TEXT
);

CREATE TABLE checklist_documentation (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    checklist_id INTEGER NOT NULL UNIQUE REFERENCES checklists (id) ON DELETE CASCADE,
    comments TEXT,
    recommendations TEXT
);

-- ---------------------------------------------------------------------------
-- 6. Заметки инженера
-- ---------------------------------------------------------------------------

CREATE TABLE engineer_notes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    author_user_id INTEGER NOT NULL REFERENCES users (id),
    facility_id INTEGER REFERENCES facilities (id),
    equipment_type_id INTEGER REFERENCES equipment_types (id),
    installation_id INTEGER REFERENCES installations (id),
    body TEXT NOT NULL,
    deadline_date TEXT,
    is_completed INTEGER NOT NULL DEFAULT 0,
    client_uuid TEXT UNIQUE,
    sync_state TEXT NOT NULL DEFAULT 'local' CHECK (sync_state IN ('local', 'pending_upload', 'synced', 'conflict')),
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX ix_engineer_notes_facility ON engineer_notes (facility_id);
CREATE INDEX ix_engineer_notes_deadline ON engineer_notes (deadline_date);

-- ---------------------------------------------------------------------------
-- 7. Выезды и расходники (заготовка)
-- ---------------------------------------------------------------------------

CREATE TABLE scheduled_visits (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    facility_id INTEGER NOT NULL REFERENCES facilities (id),
    assigned_user_id INTEGER REFERENCES users (id),
    planned_start TEXT,
    planned_end TEXT,
    notes TEXT,
    status TEXT NOT NULL DEFAULT 'planned',
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE visit_consumables (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    scheduled_visit_id INTEGER NOT NULL REFERENCES scheduled_visits (id) ON DELETE CASCADE,
    item_name TEXT NOT NULL,
    quantity REAL,
    unit TEXT,
    notes TEXT
);

-- ---------------------------------------------------------------------------
-- 8. Очередь синхронизации
-- ---------------------------------------------------------------------------

CREATE TABLE sync_outbox (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    entity_type TEXT NOT NULL,
    local_client_uuid TEXT NOT NULL,
    operation TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
    payload_json TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    processed_at TEXT,
    error_message TEXT,
    retry_count INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX ix_sync_outbox_pending ON sync_outbox (processed_at) WHERE processed_at IS NULL;
