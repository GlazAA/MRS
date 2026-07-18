namespace MRS.Application.Checklists;

/// <summary>Получение и обновление контрольного листа для 2.1.2.</summary>
public interface IChecklistEditService
{
	Task<ChecklistEditModel> GetForEditAsync(int checklistId, CancellationToken cancellationToken = default);

	/// <summary>Сухая проверка сохранения для всех редактируемых полей. Ничего не пишет в БД.</summary>
	Task<ChecklistUpdateDryRunResult> ValidateAsync(UpdateChecklistAnswersRequest request, CancellationToken cancellationToken = default);

	/// <summary>Применяет только те поля, которые разрешено сохранить (например, subset после 2.1.2.2).</summary>
	Task<ChecklistUpdateApplyResult> ApplyAsync(UpdateChecklistAnswersRequest request, IReadOnlyCollection<int> templateItemIdsToApply, CancellationToken cancellationToken = default);

	/// <summary>Обновляет статус и состояние синхронизации листа (например, при завершении).</summary>
	Task SetStatusAsync(int checklistId, string status, string syncState, CancellationToken cancellationToken = default);

	/// <summary>Создаёт лист «В работе» и фиксирует момент начала учёта времени.</summary>
	Task<int> BeginInProgressAsync(BeginInProgressChecklistRequest request, CancellationToken cancellationToken = default);

	/// <summary>Меняет установку у листа «В работе» (например, после уточнения номера новой установки).</summary>
	Task SetInstallationAsync(int checklistId, int installationId, CancellationToken cancellationToken = default);

	/// <summary>Ставит учёт времени на паузу (end_at = сейчас), статус остаётся in_progress.</summary>
	Task PauseWorkAsync(int checklistId, CancellationToken cancellationToken = default);

	/// <summary>Возобновляет учёт времени с накопленным ранее интервалом.</summary>
	Task ResumeWorkAsync(int checklistId, CancellationToken cancellationToken = default);

	/// <summary>Завершает лист и фиксирует end_at (если ещё не зафиксирован).</summary>
	Task CompleteWorkAsync(int checklistId, CancellationToken cancellationToken = default);
}

