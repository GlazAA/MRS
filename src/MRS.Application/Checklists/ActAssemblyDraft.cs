namespace MRS.Application.Checklists;

/// <summary>Профиль бланка акта (как у Brand Schutz).</summary>
public enum ActBlankProfile
{
	Installation,
	Compressor,
	Dryer
}

/// <summary>Строка перечня работ в черновике акта.</summary>
public sealed record ActWorkLine(string Label, string Mark);

/// <summary>Черновик одного акта перед скачиванием.</summary>
public sealed class ActDraft
{
	public required string DraftKey { get; init; }
	public required ActBlankProfile Profile { get; init; }
	public required string Title { get; init; }
	public required string Customer { get; init; }
	public required string WorkDates { get; init; }
	public required string InstallationLabel { get; init; }
	public required string WorkKind { get; init; }

	/// <summary>Адрес объекта для шапки акта.</summary>
	public required string ObjectAddress { get; init; }

	public string? EquipmentTypeName { get; init; }
	public string? ModelName { get; init; }
	public string? SerialNumber { get; init; }
	public string? OperatingHours { get; init; }

	/// <summary>
	/// Отображаемое значение поля состояния (обычно <c>comp_state</c>).
	/// Одно значение копируется в колонки прибытия и убытия, пока нет отдельных полей.
	/// </summary>
	public string? EquipmentStateDisplay { get; init; }

	public IReadOnlyList<ActWorkLine> WorkLines { get; init; } = [];
	public IReadOnlyList<int> SourceChecklistIds { get; init; } = [];

	/// <summary>Автосклейка; инженер может править перед скачиванием.</summary>
	public string ExtraWorksText { get; set; } = string.Empty;

	/// <summary>Автосклейка; инженер может править перед скачиванием.</summary>
	public string RemarksText { get; set; } = string.Empty;
}

/// <summary>Результат прототипа сборки: сводный акт и/или акты по единицам.</summary>
public sealed record ActAssemblyPreview(
	IReadOnlyList<ChecklistManagementRow> SourceRows,
	ActDraft? InstallationAct,
	IReadOnlyList<ActDraft> UnitActs);
