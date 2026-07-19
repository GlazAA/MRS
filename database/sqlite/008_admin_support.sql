-- Обращения инженеров к администратору БД + демо-пользователь администратора.

CREATE TABLE IF NOT EXISTS admin_support_requests (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    author_user_id INTEGER REFERENCES users (id),
    author_display_name TEXT NOT NULL,
    body TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'open' CHECK (status IN ('open', 'resolved')),
    admin_reply TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    resolved_at TEXT
);

CREATE INDEX IF NOT EXISTS ix_admin_support_requests_status ON admin_support_requests (status);
CREATE INDEX IF NOT EXISTS ix_admin_support_requests_created ON admin_support_requests (created_at);
