using System.Collections.ObjectModel;

namespace MRS.Application.Checklists;

public sealed record ChecklistEditInfo(
	int ChecklistId,
	DateTimeOffset? StartedAt,
	DateTimeOffset? EndedAt,
	string OrganizationName,
	string FacilityName,
	string EquipmentTypeName,
	string InstallationLabel,
	string MaintenanceTypeName,
	string StatusCode,
	int ChecklistTemplateId);

public sealed record ChecklistEditField(
	int TemplateItemId,
	string? FieldCode,
	string QuestionText,
	string? HintText,
	string FieldTypeName,
	bool IsLocked,
	IReadOnlyList<TemplateFieldOption> Options,
	string ValueRaw);

public sealed record ChecklistEditModel(
	ChecklistEditInfo Info,
	IReadOnlyList<ChecklistEditField> Fields);

public sealed record UpdateChecklistAnswersRequest(
	int ChecklistId,
	IReadOnlyDictionary<int, string> AnswersByTemplateItemId);

public sealed record ChecklistUpdateDryRunField(int TemplateItemId, string QuestionText);

public sealed record ChecklistUpdateDryRunResult(
	bool AllFieldsCanBeSaved,
	IReadOnlyList<ChecklistUpdateDryRunField> CanSaveFields,
	IReadOnlyList<ChecklistUpdateDryRunField> CannotSaveFields);

public sealed record ChecklistUpdateApplyResult(bool Ok, string? ErrorMessage);

