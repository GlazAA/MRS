-- 023: убрать служебных пользователей, оставшихся после 022 на уже обновлённых БД.
PRAGMA foreign_keys = ON;
DELETE FROM users;
