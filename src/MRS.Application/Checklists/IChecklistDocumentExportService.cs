namespace MRS.Application.Checklists;

/// <summary>
/// Контракт экспорта контрольного листа в документы.
/// 
/// Где используется:
/// - UI (страница "Работа с контрольными листами") вызывает ExportZipAsync(...)
/// - Инфраструктурная реализация собирает данные из SQLite и строит DOC/ZIP.
/// 
/// Как расширять:
/// - если нужен новый формат (например PDF), добавляйте новый метод в интерфейс
///   и реализуйте его в SqliteChecklistDocumentExportService.
/// </summary>
public interface IChecklistDocumentExportService
{
    /// <summary>
    /// Возвращает нормализованную модель: шапка акта + ответы шаблона.
    /// Удобно для отладки и для будущих альтернативных рендеров.
    /// </summary>
    Task<ChecklistDocumentExportModel> GetDocumentModelAsync(int checklistId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Экспорт одного контрольного листа в Word-openable HTML (.doc).
    /// </summary>
    Task<ChecklistDocumentExportFile> ExportDocAsync(int checklistId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Экспорт нескольких листов в ZIP (внутри: по одному .doc на каждый checklistId).
    /// </summary>
    Task<ChecklistDocumentExportFile> ExportZipAsync(
        IReadOnlyCollection<int> checklistIds,
        CancellationToken cancellationToken = default);
}
