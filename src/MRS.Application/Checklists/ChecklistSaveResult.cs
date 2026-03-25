namespace MRS.Application.Checklists;

public sealed record ChecklistSaveResult(bool Ok, int? ChecklistId, string? ErrorMessage);
