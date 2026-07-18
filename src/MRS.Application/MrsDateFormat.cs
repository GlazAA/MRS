using System.Globalization;

namespace MRS.Application;

/// <summary>
/// Единый формат дат для UI и документов: dd.MM.yyyy (как в русской локали календаря).
/// Для &lt;input type="date"&gt; и хранения в ответах — IsoDate (yyyy-MM-dd).
/// </summary>
public static class MrsDateFormat
{
	public const string Date = "dd.MM.yyyy";
	public const string DateShort = "dd.MM.yy";
	public const string DateTime = "dd.MM.yyyy HH:mm";
	public const string Time = "HH:mm";
	public const string IsoDate = "yyyy-MM-dd";

	private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

	public static string FormatDate(DateTimeOffset value) =>
		value.ToLocalTime().ToString(Date, Invariant);

	public static string FormatDate(DateTimeOffset? value) =>
		value is null ? "—" : FormatDate(value.Value);

	public static string FormatDate(DateTime value) =>
		value.ToString(Date, Invariant);

	public static string FormatDate(DateOnly value) =>
		value.ToString(Date, Invariant);

	public static string FormatDate(DateOnly? value) =>
		value is null ? "—" : FormatDate(value.Value);

	public static string FormatDateShort(DateTimeOffset value) =>
		value.ToLocalTime().ToString(DateShort, Invariant);

	public static string FormatDateShort(DateTimeOffset? value) =>
		value is null ? "—" : FormatDateShort(value.Value);

	public static string FormatDateShort(DateTime value) =>
		value.ToString(DateShort, Invariant);

	public static string FormatDateShort(DateOnly value) =>
		value.ToString(DateShort, Invariant);

	public static string FormatDateTime(DateTimeOffset value) =>
		value.ToLocalTime().ToString(DateTime, Invariant);

	public static string FormatDateTime(DateTimeOffset? value) =>
		value is null ? "—" : FormatDateTime(value.Value);

	public static string FormatDateTime(DateTime value) =>
		value.ToString(DateTime, Invariant);

	public static string FormatIsoDate(DateTimeOffset value) =>
		value.ToLocalTime().ToString(IsoDate, Invariant);

	public static string FormatIsoDate(DateTimeOffset? value) =>
		value is null ? string.Empty : FormatIsoDate(value.Value);

	public static string FormatTime(DateTimeOffset value) =>
		value.ToLocalTime().ToString(Time, Invariant);

	public static string FormatTime(DateTimeOffset? value) =>
		value is null ? string.Empty : FormatTime(value.Value);

	public static string FormatDateRange(DateTimeOffset? start, DateTimeOffset? end)
	{
		if (start is null && end is null)
			return "___________";
		if (start is not null && end is not null)
		{
			var s = FormatDateShort(start);
			var e = FormatDateShort(end);
			return s == e ? s : $"{s} — {e}";
		}

		return FormatDateShort(start ?? end);
	}
}
