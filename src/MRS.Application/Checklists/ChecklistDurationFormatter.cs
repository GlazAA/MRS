namespace MRS.Application.Checklists;

/// <summary>Форматирование затраченного времени контрольного листа (start_at → end_at).</summary>
public static class ChecklistDurationFormatter
{
	/// <summary>
	/// Фиксированный интервал (список КЛ, завершённые и «В работе» на паузе).
	/// Без <paramref name="endedAt"/> возвращает «—» — счётчик не идёт вне страницы заполнения.
	/// </summary>
	public static string Format(DateTimeOffset? startedAt, DateTimeOffset? endedAt)
	{
		if (startedAt is null || endedAt is null)
			return "—";

		return FormatSpan(endedAt.Value - startedAt.Value);
	}

	/// <summary>Живой счётчик — только на странице заполнения КЛ.</summary>
	public static string FormatActive(DateTimeOffset? startedAt, DateTimeOffset? now = null)
	{
		if (startedAt is null)
			return "—";

		return FormatSpan((now ?? DateTimeOffset.Now) - startedAt.Value);
	}

	public static TimeSpan Elapsed(DateTimeOffset? startedAt, DateTimeOffset? endedAt, DateTimeOffset? now = null)
	{
		if (startedAt is null)
			return TimeSpan.Zero;

		var end = endedAt ?? now ?? DateTimeOffset.Now;
		var span = end - startedAt.Value;
		return span < TimeSpan.Zero ? TimeSpan.Zero : span;
	}

	private static string FormatSpan(TimeSpan span)
	{
		if (span < TimeSpan.Zero)
			span = TimeSpan.Zero;

		var hours = (int)span.TotalHours;
		return $"{hours}:{span.Minutes:D2}:{span.Seconds:D2}";
	}
}
