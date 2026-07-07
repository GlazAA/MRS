-- Дополнительные поля для синхронизации шаблонов с SQLite.

ALTER TABLE checklist_templates ADD COLUMN IF NOT EXISTS intro_modal_text TEXT;
