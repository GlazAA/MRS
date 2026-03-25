namespace MRS.Application.Checklists;

/// <summary>Вариант развилки: вид ТО + связанный шаблон контрольного листа.</summary>
public sealed record MaintenanceForkOption(
	int MaintenanceTypeId,
	string MaintenanceTypeName,
	string? MaintenanceCode,
	int ChecklistTemplateId,
	string TemplateName,
	string? ScenarioCode);
