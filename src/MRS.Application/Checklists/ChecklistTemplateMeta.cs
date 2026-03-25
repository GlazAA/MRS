namespace MRS.Application.Checklists;

public sealed record ChecklistTemplateMeta(
	string TemplateName,
	string? TopPlateText,
	string? IntroModalText,
	string? SafetyModalText,
	bool RedButtonEnabled);
