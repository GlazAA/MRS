-- Юридическая форма организации (полевой ввод + акт).
ALTER TABLE organizations ADD COLUMN legal_form_code TEXT;

INSERT OR IGNORE INTO ownership_forms (id, code, full_name, short_name) VALUES
    (2, 'IP', 'Индивидуальный предприниматель', 'ИП'),
    (3, 'SELF_EMPLOYED', 'Самозанятый', 'самозанятый'),
    (4, 'AO', 'Акционерное общество', 'АО'),
    (5, 'ZAO', 'Закрытое акционерное общество', 'ЗАО');
