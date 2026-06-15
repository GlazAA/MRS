namespace MRS.Application.Checklists;

/// <summary>Поля шаблона, связанные с датой/временем работ — управляются сессией учёта, не формой.</summary>
public static class ChecklistWorkSessionFieldCodes
{
	private static readonly HashSet<string> TimingCodes = new(StringComparer.OrdinalIgnoreCase)
	{
		"start_date",
		"end_date",
		"start_time",
		"end_time"
	};

	public static bool IsTimingField(string? fieldCode) =>
		fieldCode is not null && TimingCodes.Contains(fieldCode);
}
