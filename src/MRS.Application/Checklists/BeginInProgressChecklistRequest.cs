namespace MRS.Application.Checklists;

public sealed record BeginInProgressChecklistRequest(
	int InstallationId,
	int ChecklistTemplateId,
	int MaintenanceTypeId,
	int EngineerUserId,
	DateTimeOffset WorkStartedAt);
