namespace MRS.Application.Checklists;

// Единая "шапка" документа (верхний фиксированный блок и часть полей акта).
// Важно: это уже подготовленные доменные данные, без SQL-зависимостей.
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

// Одна строка ответа из шаблона.
// ValueRaw хранит "техническое" значение, ValueDisplay — значение для печати/документа.
public sealed record ChecklistDocumentAnswer(
    int TemplateItemId,
    int SortOrder,
    string? FieldCode,
    string QuestionText,
    string FieldTypeName,
    string ValueRaw,
    string ValueDisplay);

// Полная модель, из которой строится DOC.
// Header -> постоянная часть + базовые реквизиты.
// Answers -> переменная часть (состояние/перечень работ и пр.).
public sealed record ChecklistDocumentExportModel(
    ChecklistDocumentHeader Header,
    IReadOnlyList<ChecklistDocumentAnswer> Answers);

// Универсальный файл результата для отдачи в UI (имя + MIME + байты).
public sealed record ChecklistDocumentExportFile(
    string FileName,
    string MimeType,
    byte[] Content);
