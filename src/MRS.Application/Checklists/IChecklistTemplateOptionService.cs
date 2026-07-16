namespace MRS.Application.Checklists;

public interface IChecklistTemplateOptionService
{
	/// <summary>Находит вариант по подписи (без учёта регистра) или создаёт новый.</summary>
	Task<TemplateFieldOption> EnsureOptionAsync(
		int templateItemId,
		string label,
		CancellationToken cancellationToken = default);
}
