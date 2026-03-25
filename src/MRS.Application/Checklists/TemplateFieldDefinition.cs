namespace MRS.Application.Checklists;

public sealed record TemplateFieldDefinition(
	int TemplateItemId,
	int SortOrder,
	string? FieldCode,
	string QuestionText,
	string? HintText,
	string FieldTypeName,
	bool IsRequired,
	IReadOnlyList<TemplateFieldOption> Options);
