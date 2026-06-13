using MRS.Application.Facilities;

namespace MRS.Application.Checklists;

/// <summary>
/// Конструктор шаблонов контрольных листов:
/// </summary>
public interface IChecklistTemplateAuthoringService
{
    Task<IReadOnlyList<HierarchyOption>> GetEquipmentTypesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MaintenanceTypeOption>> GetMaintenanceTypesAsync(CancellationToken cancellationToken = default);

    Task<int> CreateTemplateAsync(CreateChecklistTemplateRequest request, CancellationToken cancellationToken = default);
}

public sealed record MaintenanceTypeOption(int Id, string Name, string? Code);

public sealed record CreateChecklistTemplateRequest(
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
