namespace MRS.Application.Checklists;

/// <summary>
/// Локальный черновик — только на устройстве. Очередь на сервер — завершённый лист для последующей выгрузки.
/// </summary>
public enum ChecklistPersistenceMode
{
	LocalDraft,
	UploadQueue
}
