namespace MRS.Application.Checklists;

/// <summary>Сводка уже созданных листов по установкам внутри системы (только чтение).</summary>
public interface IChecklistSummaryService
{
	Task<IReadOnlyList<ChecklistSummaryRow>> GetForSystemAsync(int facilitySystemId, int limit = 25, CancellationToken cancellationToken = default);
}
