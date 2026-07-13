-- Синхронизация: блокировки при pending push, client_uuid для выездов.

CREATE TABLE IF NOT EXISTS sync_entity_locks (
    outbox_id INTEGER NOT NULL REFERENCES sync_outbox (id) ON DELETE CASCADE,
    entity_type TEXT NOT NULL,
    local_id INTEGER NOT NULL,
    PRIMARY KEY (entity_type, local_id)
);

CREATE INDEX IF NOT EXISTS ix_sync_entity_locks_outbox ON sync_entity_locks (outbox_id);

ALTER TABLE scheduled_visits ADD COLUMN client_uuid TEXT;
ALTER TABLE scheduled_visits ADD COLUMN sync_state TEXT NOT NULL DEFAULT 'synced'
    CHECK (sync_state IN ('local', 'pending_upload', 'synced', 'conflict'));

CREATE UNIQUE INDEX IF NOT EXISTS ux_scheduled_visits_client_uuid ON scheduled_visits (client_uuid)
    WHERE client_uuid IS NOT NULL;
