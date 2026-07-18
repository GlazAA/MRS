using MRS.Application.Facilities;

namespace MRS.Application.Checklists;

/// <summary>
/// Конструктор шаблонов контрольных листов:
/// </summary>
public interface IChecklistTemplateAuthoringService
{
	Task<IReadOnlyList<HierarchyOption>> GetEquipmentTypesAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MaintenanceTypeOption>> GetMaintenanceTypesAsync(CancellationToken cancellationToken = default);

	/// <summary>Список активных шаблонов для копирования «по аналогии».</summary>
	Task<IReadOnlyList<TemplateCloneSourceOption>> ListTemplatesForCloneAsync(CancellationToken cancellationToken = default);

	/// <summary>Черновик полей и текстов выбранного шаблона-донора.</summary>
	Task<TemplateCloneDraft> GetTemplateCloneDraftAsync(int templateId, CancellationToken cancellationToken = default);

	Task<int> CreateTemplateAsync(CreateChecklistTemplateRequest request, CancellationToken cancellationToken = default);
}

public sealed record MaintenanceTypeOption(int Id, string Name, string? Code);

public sealed record TemplateCloneSourceOption(
	int TemplateId,
	string TemplateName,
	int EquipmentTypeId,
	string EquipmentTypeName,
	int MaintenanceTypeId,
	string MaintenanceTypeName,
	int? FacilityId,
	string? FacilityName,
	string? OrganizationName,
	int FieldCount);

public sealed record TemplateCloneDraft(
	int TemplateId,
	string TemplateName,
	int EquipmentTypeId,
	int MaintenanceTypeId,
	int? FacilityId,
	int? OrganizationId,
	string? TopPlateText,
	string? SafetyModalText,
	bool RedButtonEnabled,
	IReadOnlyList<TemplateCloneFieldDraft> Fields);

public sealed record TemplateCloneFieldDraft(
	int SortOrder,
	string? FieldCode,
	string QuestionText,
	string? HintText,
	string FieldTypeName,
	bool IsRequired,
	IReadOnlyList<string> Options);

public sealed record CreateChecklistTemplateRequest(
	int FacilityId,
	int EquipmentTypeId,
	int? ExistingMaintenanceTypeId,
	string? NewMaintenanceTypeName,
	string? NewMaintenanceTypeCode,
	string? NewMaintenanceTypeDescription,
	string TemplateName,
	string? ScenarioCode,
	string? TopPlateText,
	string? IntroModalText,
	string? SafetyModalText,
	bool RedButtonEnabled,
	IReadOnlyList<CreateTemplateFieldRequest> Fields);

public sealed record CreateTemplateFieldRequest(
	int SortOrder,
	string? FieldCode,
	string QuestionText,
	string? HintText,
	string FieldTypeName,
	bool IsRequired,
	string? GroupName,
	string? ValidationRuleCode,
	IReadOnlyList<string> Options);
