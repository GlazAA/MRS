-- Поля для внесения объекта: строение в адресе, адрес по договору, переименование систем.
ALTER TABLE organization_addresses ADD COLUMN structure TEXT;

ALTER TABLE facilities ADD COLUMN contract_address TEXT;

UPDATE facility_systems
SET name = 'Гипоксическая система предотвращения пожара'
WHERE TRIM(name) = 'Система пожаротушения';
