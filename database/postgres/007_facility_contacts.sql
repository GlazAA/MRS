-- Привязка контакта заказчика к объекту (как в SQLite v16).
ALTER TABLE organization_employees
    ADD COLUMN IF NOT EXISTS facility_id BIGINT REFERENCES facilities (id);

CREATE INDEX IF NOT EXISTS ix_organization_employees_facility
    ON organization_employees (facility_id);

UPDATE organization_employees e
SET facility_id = sub.facility_id
FROM (
    SELECT DISTINCT ON (emp_id) emp_id, facility_id
    FROM (
        SELECT responsible_employee_id AS emp_id, id AS facility_id FROM facilities WHERE responsible_employee_id IS NOT NULL
        UNION ALL
        SELECT secondary_contact_id, id FROM facilities WHERE secondary_contact_id IS NOT NULL
    ) x
    ORDER BY emp_id, facility_id
) sub
WHERE e.id = sub.emp_id AND e.facility_id IS NULL;
