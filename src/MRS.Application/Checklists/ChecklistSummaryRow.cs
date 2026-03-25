namespace MRS.Application.Checklists;

public sealed record ChecklistSummaryRow(
	int ChecklistId,
	string EquipmentTypeName,
	DateTimeOffset? StartedAt,
	string StatusCode);
