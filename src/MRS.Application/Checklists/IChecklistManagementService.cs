namespace MRS.Application.Checklists;

/// <summary>Список контрольных листов для 2.1.1 (с сортировкой и экспортом).</summary>
public interface IChecklistManagementService
{
	Task<IReadOnlyList<ChecklistManagementRow>> GetAllAsync(CancellationToken cancellationToken = default);
}

