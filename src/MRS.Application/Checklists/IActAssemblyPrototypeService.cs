namespace MRS.Application.Checklists;

/// <summary>
/// Прототип сборки актов: из нескольких КЛ — черновики бланков
/// «установка» / «компрессор» / «осушитель» с автосклейкой доп. работ и замечаний.
/// </summary>
public interface IActAssemblyPrototypeService
{
	/// <summary>Собирает черновики по выбранным контрольным листам.</summary>
	Task<ActAssemblyPreview> BuildPreviewAsync(
		IReadOnlyList<int> checklistIds,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Готовый демо-черновик без БД (по образцам Brand Schutz), чтобы проверить UX без заполненных КЛ.
	/// </summary>
	ActAssemblyPreview BuildDemoPreview();

	/// <summary>Рендерит простой .doc (HTML) из отредактированного черновика.</summary>
	ChecklistDocumentExportFile RenderDraftDoc(ActDraft draft);

	/// <summary>Рендерит PDF из отредактированного черновика.</summary>
	ChecklistDocumentExportFile RenderDraftPdf(ActDraft draft);

	/// <summary>Упаковывает несколько актов в один ZIP.</summary>
	ChecklistDocumentExportFile RenderDraftsZip(IReadOnlyList<ActDraft> drafts);
}
