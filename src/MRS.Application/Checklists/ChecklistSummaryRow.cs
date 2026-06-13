namespace MRS.Application.Checklists;

public sealed record ChecklistSummaryRow(
	int ChecklistId,
	string EquipmentTypeName,
	int EquipmentTypeId,
	int MaintenanceTypeId,
	DateTimeOffset? StartedAt,
	DateTimeOffset? EndedAt,
	string StatusCode);
