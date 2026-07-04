using System.Globalization;

namespace MRS.Application.Checklists;

/// <summary>Дата и время в часовом поясе Москвы (Europe/Moscow).</summary>
public static class MoscowRegionalClock
{
	private static readonly TimeZoneInfo MoscowTimeZone = ResolveMoscowTimeZone();

	public static DateOnly TodayDateOnly =>
		DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, MoscowTimeZone).DateTime);

	public static string TodayIsoDate =>
		TodayDateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

	private static TimeZoneInfo ResolveMoscowTimeZone()
	{
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
		}
		catch (TimeZoneNotFoundException)
		{
			return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
		}
	}
}
