namespace MRS.Application.Checklists;

/// <summary>Развилки по типу оборудования и загрузка шаблона для формы создания листа.</summary>
public interface IChecklistFlowService
{
	/// <summary>
	/// Виды ТО и шаблоны для типа оборудования на объекте.
	/// Сначала объектные шаблоны, иначе общие (facility_id IS NULL).
	/// </summary>
	Task<IReadOnlyList<MaintenanceForkOption>> GetMaintenanceForkAsync(
		int equipmentTypeId,
		int? facilityId = null,
		CancellationToken cancellationToken = default);

	Task<int?> ResolveTemplateIdAsync(
		int equipmentTypeId,
		int maintenanceTypeId,
		int? facilityId = null,
		CancellationToken cancellationToken = default);

	Task<ChecklistTemplateMeta?> GetTemplateMetaAsync(int checklistTemplateId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<TemplateFieldDefinition>> GetTemplateFieldsAsync(int checklistTemplateId, CancellationToken cancellationToken = default);
}
