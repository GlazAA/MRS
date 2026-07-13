-- v16: контакт заказчика привязан к объекту (facility), ФИО уже отдельными полями.

ALTER TABLE organization_employees ADD COLUMN facility_id INTEGER REFERENCES facilities (id);

CREATE INDEX IF NOT EXISTS ix_organization_employees_facility
    ON organization_employees (facility_id);

-- Привязать демо-контакты к объектам через ответственных на facility.
UPDATE organization_employees
SET facility_id = (
    SELECT f.id
    FROM facilities f
    WHERE f.responsible_employee_id = organization_employees.id
       OR f.secondary_contact_id = organization_employees.id
    ORDER BY f.id
    LIMIT 1
)
WHERE facility_id IS NULL;
