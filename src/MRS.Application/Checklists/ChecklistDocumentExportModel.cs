namespace MRS.Application.Checklists;

public sealed record ChecklistDocumentHeader(
    int ChecklistId,
    string OrganizationName,
    string FacilityName,
    string EquipmentTypeName,
    string InstallationLabel,
    string MaintenanceTypeName,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    string StatusCode);

public sealed record ChecklistDocumentAnswer(
    int TemplateItemId,
    int SortOrder,
    string? FieldCode,
    string QuestionText,
    string FieldTypeName,
    string ValueRaw,
    string ValueDisplay);

public sealed record ChecklistDocumentExportModel(
    ChecklistDocumentHeader Header,
    IReadOnlyList<ChecklistDocumentAnswer> Answers);

public sealed record ChecklistDocumentExportFile(
    string FileName,
    string MimeType,
    byte[] Content);
