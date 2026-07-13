-- client_uuid для выездов (синхронизация и merge по UUID).

ALTER TABLE scheduled_visits ADD COLUMN IF NOT EXISTS client_uuid UUID UNIQUE;
