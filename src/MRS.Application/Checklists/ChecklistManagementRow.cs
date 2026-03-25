namespace MRS.Application.Checklists;

public sealed record ChecklistManagementRow(
	int ChecklistId,
	DateTimeOffset? StartedAt,
	string OrganizationName,
	string FacilityName,
	string EquipmentTypeName,
	string InstallationLabel,
	string MaintenanceTypeName,
	string StatusCode);

