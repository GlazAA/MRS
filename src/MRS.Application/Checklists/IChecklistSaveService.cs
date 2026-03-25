namespace MRS.Application.Checklists;

/// <summary>Запись контрольного листа и ответов в SQLite.</summary>
public interface IChecklistSaveService
{
	Task<ChecklistSaveResult> SaveAsync(SaveChecklistRequest request, CancellationToken cancellationToken = default);
}
