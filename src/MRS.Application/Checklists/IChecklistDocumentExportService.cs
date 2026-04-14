namespace MRS.Application.Checklists;

/// <summary>
/// Экспорт заполненного контрольного листа в файл и доступ к нормализованной модели данных.
/// </summary>
public interface IChecklistDocumentExportService
{
    Task<ChecklistDocumentExportModel> GetDocumentModelAsync(int checklistId, CancellationToken cancellationToken = default);

    Task<ChecklistDocumentExportFile> ExportDocAsync(int checklistId, CancellationToken cancellationToken = default);

    Task<ChecklistDocumentExportFile> ExportZipAsync(
        IReadOnlyCollection<int> checklistIds,
        CancellationToken cancellationToken = default);
}
