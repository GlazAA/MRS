-- Все 13 типов оборудования (EQ-01…EQ-13) доступны в каждой из трёх демо-систем (1, 2, 3).

DELETE FROM system_equipment_types WHERE system_id IN (1, 2, 3);

INSERT INTO system_equipment_types (system_id, equipment_type_id)
SELECT s.n, et.id
FROM equipment_types et
CROSS JOIN (
    SELECT 1 AS n
    UNION ALL SELECT 2
    UNION ALL SELECT 3
) s
ORDER BY s.n, et.id;
