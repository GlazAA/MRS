namespace MRS.Application.Checklists;

/// <summary>Получение и обновление контрольного листа для 2.1.2.</summary>
public interface IChecklistEditService
{
	Task<ChecklistEditModel> GetForEditAsync(int checklistId, CancellationToken cancellationToken = default);

	/// <summary>Сухая проверка сохранения для всех редактируемых полей. Ничего не пишет в БД.</summary>
	Task<ChecklistUpdateDryRunResult> ValidateAsync(UpdateChecklistAnswersRequest request, CancellationToken cancellationToken = default);

	/// <summary>Применяет только те поля, которые разрешено сохранить (например, subset после 2.1.2.2).</summary>
	Task<ChecklistUpdateApplyResult> ApplyAsync(UpdateChecklistAnswersRequest request, IReadOnlyCollection<int> templateItemIdsToApply, CancellationToken cancellationToken = default);
}

