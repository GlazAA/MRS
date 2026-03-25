-- MRS: схема PostgreSQL для контрольных листов ТО (пожаротушение / сопутствующее оборудование).
-- Идентификаторы: BIGSERIAL. Даты/время: TIMESTAMPTZ (UTC).
--
-- Сценарии без выбора вида ТО в UI (спецификация 1.9–1.20): в БД завязывайте шаблон на maintenance_types.code = 'INT-UNIFIED'.
-- facilities.ui_flow: object_only — упрощённый поток (Мосархив–Сахарово); hierarchical — компания → объект.
-- Комбобокс «из справочника или вручную»: значение из списка — selected_option_id; произвольный ввод — text_response.

-- ---------------------------------------------------------------------------
-- 1. Пользователи, роли, безопасность
-- ---------------------------------------------------------------------------

CREATE TABLE user_roles (
    id BIGSERIAL PRIMARY KEY,
    role_name TEXT NOT NULL UNIQUE
);

CREATE TABLE users (
    id BIGSERIAL PRIMARY KEY,
    user_role_id BIGINT NOT NULL REFERENCES user_roles (id),
    first_name TEXT,
    last_name TEXT,
    middle_name TEXT,
    login TEXT NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    email TEXT,
    phone_number TEXT,
    phone_number_secondary TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    failed_login_count INTEGER NOT NULL DEFAULT 0,
    locked_until TIMESTAMPTZ,
    password_changed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE user_personal_data (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL UNIQUE REFERENCES users (id) ON DELETE CASCADE,
    passport_series TEXT,
    passport_number TEXT,
    passport_issued_by TEXT,
    passport_issue_date DATE,
    passport_department_code TEXT
);

CREATE TABLE user_refresh_tokens (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    token_hash TEXT NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    revoked_at TIMESTAMPTZ,
    device_id TEXT,
    user_agent TEXT
);

CREATE INDEX ix_user_refresh_tokens_user_id ON user_refresh_tokens (user_id);
CREATE INDEX ix_user_refresh_tokens_expires ON user_refresh_tokens (expires_at) WHERE revoked_at IS NULL;

-- ---------------------------------------------------------------------------
-- 2. Организации и сотрудники заказчика
-- ---------------------------------------------------------------------------

CREATE TABLE banks (
    id BIGSERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    bic TEXT,
    correspondent_account TEXT
);

CREATE TABLE organization_addresses (
    id BIGSERIAL PRIMARY KEY,
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
    id BIGSERIAL PRIMARY KEY,
    code TEXT NOT NULL UNIQUE,
    full_name TEXT NOT NULL,
    short_name TEXT NOT NULL
);

CREATE TABLE organizations (
    id BIGSERIAL PRIMARY KEY,
    full_name TEXT NOT NULL,
    short_name TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE organization_history (
    id BIGSERIAL PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organizations (id) ON DELETE CASCADE,
    full_name TEXT NOT NULL,
    change_date DATE NOT NULL,
    change_reason TEXT
);

CREATE TABLE organization_data (
    id BIGSERIAL PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organizations (id) ON DELETE CASCADE,
    legal_address_id BIGINT NOT NULL REFERENCES organization_addresses (id),
    ownership_form_id BIGINT NOT NULL REFERENCES ownership_forms (id),
    inn TEXT NOT NULL
        CHECK (inn ~ '^[0-9]{10}$' OR inn ~ '^[0-9]{12}$'),
    kpp TEXT
        CHECK (kpp IS NULL OR kpp ~ '^[0-9]{9}$'),
    ogrn TEXT NOT NULL
        CHECK (ogrn ~ '^[0-9]{13}$' OR ogrn ~ '^[0-9]{15}$'),
    bank_id BIGINT REFERENCES banks (id),
    payment_account TEXT,
    ceo_full_name TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE organization_employees (
    id BIGSERIAL PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organizations (id),
    first_name TEXT NOT NULL,
    last_name TEXT NOT NULL,
    middle_name TEXT,
    position TEXT,
    work_phone TEXT,
    work_phone_secondary TEXT,
    work_email TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

-- ---------------------------------------------------------------------------
-- 3. Объекты, системы, установки
-- ---------------------------------------------------------------------------

CREATE TABLE facilities (
    id BIGSERIAL PRIMARY KEY,
    organization_id BIGINT NOT NULL REFERENCES organizations (id),
    responsible_employee_id BIGINT REFERENCES organization_employees (id),
    secondary_contact_id BIGINT REFERENCES organization_employees (id),
    name TEXT NOT NULL,
    address_id BIGINT NOT NULL REFERENCES organization_addresses (id),
    ui_flow TEXT NOT NULL DEFAULT 'hierarchical'
        CHECK (ui_flow IN ('hierarchical', 'object_only')),
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE facility_systems (
    id BIGSERIAL PRIMARY KEY,
    facility_id BIGINT NOT NULL REFERENCES facilities (id),
    name TEXT NOT NULL,
    description TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE equipment_models (
    id BIGSERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    manufacturer TEXT,
    specifications TEXT
);

CREATE TABLE equipment_types (
    id BIGSERIAL PRIMARY KEY,
    type_name TEXT NOT NULL UNIQUE,
    code TEXT UNIQUE
);

CREATE TABLE system_equipment_types (
    system_id BIGINT NOT NULL REFERENCES facility_systems (id) ON DELETE CASCADE,
    equipment_type_id BIGINT NOT NULL REFERENCES equipment_types (id) ON DELETE CASCADE,
    PRIMARY KEY (system_id, equipment_type_id)
);

CREATE TABLE installations (
    id BIGSERIAL PRIMARY KEY,
    system_id BIGINT NOT NULL REFERENCES facility_systems (id),
    equipment_type_id BIGINT NOT NULL REFERENCES equipment_types (id),
    equipment_model_id BIGINT REFERENCES equipment_models (id),
    custom_name TEXT,
    standard_model_name TEXT,
    standard_serial_number TEXT,
    custom_model_name TEXT,
    custom_serial_number TEXT,
    is_data_modified BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

-- ---------------------------------------------------------------------------
-- 4. Типы ТО, поля, валидация, шаблоны
-- ---------------------------------------------------------------------------

CREATE TABLE maintenance_types (
    id BIGSERIAL PRIMARY KEY,
    type_name TEXT NOT NULL,
    code TEXT UNIQUE,
    description TEXT,
    recommended_interval_days INTEGER
);

CREATE TABLE field_types (
    id BIGSERIAL PRIMARY KEY,
    type_name TEXT NOT NULL UNIQUE
);

CREATE TABLE validation_rules (
    id BIGSERIAL PRIMARY KEY,
    code TEXT NOT NULL UNIQUE,
    description TEXT,
    error_message TEXT
);

CREATE TABLE checklist_templates (
    id BIGSERIAL PRIMARY KEY,
    equipment_type_id BIGINT NOT NULL REFERENCES equipment_types (id),
    maintenance_type_id BIGINT NOT NULL REFERENCES maintenance_types (id),
    template_name TEXT NOT NULL,
    scenario_code TEXT UNIQUE,
    version INTEGER NOT NULL DEFAULT 1,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    top_plate_text TEXT,
    safety_modal_text TEXT,
    red_button_enabled BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE checklist_template_items (
    id BIGSERIAL PRIMARY KEY,
    checklist_template_id BIGINT NOT NULL REFERENCES checklist_templates (id) ON DELETE CASCADE,
    sort_order INTEGER NOT NULL,
    field_code TEXT,
    question_text TEXT NOT NULL,
    hint_text TEXT,
    field_type_id BIGINT NOT NULL REFERENCES field_types (id),
    validation_rule_code TEXT REFERENCES validation_rules (code),
    is_required BOOLEAN NOT NULL DEFAULT FALSE,
    visible_when_maintenance_type_code TEXT,
    group_name TEXT
);

CREATE INDEX ix_checklist_template_items_template ON checklist_template_items (checklist_template_id);

CREATE TABLE checklist_template_item_options (
    id BIGSERIAL PRIMARY KEY,
    checklist_template_item_id BIGINT NOT NULL REFERENCES checklist_template_items (id) ON DELETE CASCADE,
    sort_order INTEGER NOT NULL,
    option_code TEXT,
    option_label TEXT NOT NULL
);

CREATE INDEX ix_template_item_options_item ON checklist_template_item_options (checklist_template_item_id);

-- ---------------------------------------------------------------------------
-- 5. Контрольные листы и ответы
-- ---------------------------------------------------------------------------

CREATE TABLE checklist_participant_roles (
    id BIGSERIAL PRIMARY KEY,
    code TEXT NOT NULL UNIQUE,
    display_name TEXT NOT NULL
);

CREATE TABLE checklists (
    id BIGSERIAL PRIMARY KEY,
    installation_id BIGINT NOT NULL REFERENCES installations (id),
    maintenance_type_id BIGINT NOT NULL REFERENCES maintenance_types (id),
    checklist_template_id BIGINT REFERENCES checklist_templates (id),
    engineer_id BIGINT NOT NULL REFERENCES users (id),
    responsible_employee_id BIGINT REFERENCES organization_employees (id),
    start_at TIMESTAMPTZ,
    end_at TIMESTAMPTZ,
    status TEXT NOT NULL DEFAULT 'draft'
        CHECK (status IN ('draft', 'in_progress', 'completed', 'cancelled')),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    client_uuid UUID UNIQUE,
    sync_state TEXT NOT NULL DEFAULT 'local'
        CHECK (sync_state IN ('local', 'pending_upload', 'synced', 'conflict')),
    server_updated_at TIMESTAMPTZ,
    local_updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX ix_checklists_installation ON checklists (installation_id);
CREATE INDEX ix_checklists_engineer ON checklists (engineer_id);
CREATE INDEX ix_checklists_sync ON checklists (sync_state);

CREATE TABLE checklist_participants (
    id BIGSERIAL PRIMARY KEY,
    checklist_id BIGINT NOT NULL REFERENCES checklists (id) ON DELETE CASCADE,
    role_id BIGINT NOT NULL REFERENCES checklist_participant_roles (id),
    user_id BIGINT REFERENCES users (id),
    organization_employee_id BIGINT REFERENCES organization_employees (id),
    display_name_snapshot TEXT,
    sort_order INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX ix_checklist_participants_checklist ON checklist_participants (checklist_id);

CREATE TABLE checklist_responses (
    id BIGSERIAL PRIMARY KEY,
    checklist_id BIGINT NOT NULL REFERENCES checklists (id) ON DELETE CASCADE,
    checklist_template_item_id BIGINT NOT NULL REFERENCES checklist_template_items (id),
    boolean_response BOOLEAN,
    text_response TEXT,
    numeric_response DOUBLE PRECISION,
    selected_option_id BIGINT REFERENCES checklist_template_item_options (id),
    UNIQUE (checklist_id, checklist_template_item_id)
);

CREATE INDEX ix_checklist_responses_checklist ON checklist_responses (checklist_id);

-- Множественный выбор (checkbox group): несколько опций на один ответ-строку.
CREATE TABLE checklist_response_multi_options (
    checklist_response_id BIGINT NOT NULL REFERENCES checklist_responses (id) ON DELETE CASCADE,
    checklist_template_item_option_id BIGINT NOT NULL REFERENCES checklist_template_item_options (id) ON DELETE CASCADE,
    PRIMARY KEY (checklist_response_id, checklist_template_item_option_id)
);

CREATE TABLE maintenance_history (
    id BIGSERIAL PRIMARY KEY,
    installation_id BIGINT NOT NULL REFERENCES installations (id),
    checklist_id BIGINT REFERENCES checklists (id),
    maintenance_type_id BIGINT NOT NULL REFERENCES maintenance_types (id),
    maintenance_date DATE NOT NULL,
    next_maintenance_date DATE,
    engineer_id BIGINT REFERENCES users (id),
    comments TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE media_files (
    id BIGSERIAL PRIMARY KEY,
    checklist_id BIGINT NOT NULL REFERENCES checklists (id) ON DELETE CASCADE,
    file_path TEXT NOT NULL,
    file_name TEXT,
    mime_type TEXT,
    description TEXT
);

CREATE TABLE checklist_documentation (
    id BIGSERIAL PRIMARY KEY,
    checklist_id BIGINT NOT NULL UNIQUE REFERENCES checklists (id) ON DELETE CASCADE,
    comments TEXT,
    recommendations TEXT
);

-- ---------------------------------------------------------------------------
-- 6. Заметки инженера (экран «Просмотр комментариев»)
-- ---------------------------------------------------------------------------

CREATE TABLE engineer_notes (
    id BIGSERIAL PRIMARY KEY,
    author_user_id BIGINT NOT NULL REFERENCES users (id),
    facility_id BIGINT REFERENCES facilities (id),
    equipment_type_id BIGINT REFERENCES equipment_types (id),
    installation_id BIGINT REFERENCES installations (id),
    body TEXT NOT NULL,
    deadline_date DATE,
    is_completed BOOLEAN NOT NULL DEFAULT FALSE,
    client_uuid UUID UNIQUE,
    sync_state TEXT NOT NULL DEFAULT 'local'
        CHECK (sync_state IN ('local', 'pending_upload', 'synced', 'conflict')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX ix_engineer_notes_facility ON engineer_notes (facility_id);
CREATE INDEX ix_engineer_notes_deadline ON engineer_notes (deadline_date);

-- ---------------------------------------------------------------------------
-- 7. Заготовки под выезды и расходники (из макета — «не реализовано»)
-- ---------------------------------------------------------------------------

CREATE TABLE scheduled_visits (
    id BIGSERIAL PRIMARY KEY,
    facility_id BIGINT NOT NULL REFERENCES facilities (id),
    assigned_user_id BIGINT REFERENCES users (id),
    planned_start TIMESTAMPTZ,
    planned_end TIMESTAMPTZ,
    notes TEXT,
    status TEXT NOT NULL DEFAULT 'planned',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE visit_consumables (
    id BIGSERIAL PRIMARY KEY,
    scheduled_visit_id BIGINT NOT NULL REFERENCES scheduled_visits (id) ON DELETE CASCADE,
    item_name TEXT NOT NULL,
    quantity NUMERIC(18, 4),
    unit TEXT,
    notes TEXT
);

-- ---------------------------------------------------------------------------
-- 8. Очередь синхронизации клиент → сервер (опционально, для MAUI)
-- ---------------------------------------------------------------------------

CREATE TABLE sync_outbox (
    id BIGSERIAL PRIMARY KEY,
    entity_type TEXT NOT NULL,
    local_client_uuid UUID NOT NULL,
    operation TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
    payload_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processed_at TIMESTAMPTZ,
    error_message TEXT,
    retry_count INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX ix_sync_outbox_pending ON sync_outbox (created_at) WHERE processed_at IS NULL;
