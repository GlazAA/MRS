namespace MRS.Application.Checklists;

public static class ChecklistFieldCodes
{
	public const string EndDate = "end_date";
	public const string EndTime = "end_time";

	public static bool IsEndTime(string? fieldCode) =>
		fieldCode is not null && fieldCode.Equals(EndTime, StringComparison.OrdinalIgnoreCase);
}
