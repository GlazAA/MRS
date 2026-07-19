namespace MRS.Application.Checklists;

/// <summary>Правила блокировки полей при редактировании сохранённого КЛ.</summary>
public static class ChecklistEditFieldRules
{
	private static readonly HashSet<string> StructuralFieldCodes = new(StringComparer.OrdinalIgnoreCase)
	{
		"equipment_pick"
	};

	public static bool IsLocked(string? fieldCode) =>
		fieldCode is not null && (
			StructuralFieldCodes.Contains(fieldCode) ||
			ChecklistWorkSessionFieldCodes.IsTimingField(fieldCode));

	public static string GetLockReason(string? fieldCode)
	{
		if (fieldCode is null)
			return "Системное поле";

		if (ChecklistWorkSessionFieldCodes.IsTimingField(fieldCode))
			return "Управляется учётом времени работы";

		if (fieldCode.Equals("equipment_pick", StringComparison.OrdinalIgnoreCase))
			return "Тип оборудования задан при выборе шаблона и подставляется автоматически";

		return "Системное поле";
	}

	public static bool ValuesEqual(string? a, string? b) =>
		string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);

	private static string Normalize(string? value) => (value ?? string.Empty).Trim();
}
