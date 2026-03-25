namespace MRS.Application.Checklists;

/// <summary>Ответы: ключ — id пункта шаблона (<c>checklist_template_items.id</c>), значение — строка (текст, id опции или несколько id через запятую для множественного выбора).</summary>
public sealed record SaveChecklistRequest(
	int InstallationId,
	int ChecklistTemplateId,
	int MaintenanceTypeId,
	int EngineerUserId,
	IReadOnlyDictionary<int, string> AnswersByTemplateItemId,
	ChecklistPersistenceMode PersistenceMode);
