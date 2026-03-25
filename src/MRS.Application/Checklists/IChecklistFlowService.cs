namespace MRS.Application.Checklists;

/// <summary>Развилки по типу оборудования и загрузка шаблона для формы создания листа.</summary>
public interface IChecklistFlowService
{
	/// <summary>Виды ТО и шаблоны, доступные для выбранного типа оборудования (из <c>checklist_templates</c>).</summary>
	Task<IReadOnlyList<MaintenanceForkOption>> GetMaintenanceForkAsync(int equipmentTypeId, CancellationToken cancellationToken = default);

	Task<int?> ResolveTemplateIdAsync(int equipmentTypeId, int maintenanceTypeId, CancellationToken cancellationToken = default);

	Task<ChecklistTemplateMeta?> GetTemplateMetaAsync(int checklistTemplateId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<TemplateFieldDefinition>> GetTemplateFieldsAsync(int checklistTemplateId, CancellationToken cancellationToken = default);
}
