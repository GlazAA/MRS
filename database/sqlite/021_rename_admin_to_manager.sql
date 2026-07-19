-- 021: роль «Администратор» → «Менеджер»
PRAGMA foreign_keys = ON;

UPDATE user_roles
SET role_name = 'Менеджер'
WHERE role_name = 'Администратор';

-- На случай расхождения id/имени в старых копиях.
UPDATE user_roles
SET role_name = 'Менеджер'
WHERE id = 2 AND role_name <> 'Менеджер' AND role_name <> 'Администратор БД';
