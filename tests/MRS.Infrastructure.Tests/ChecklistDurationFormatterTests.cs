using MRS.Application.Checklists;

namespace MRS.Infrastructure.Tests;

public class ChecklistDurationFormatterTests
{
	[Theory]
	[InlineData(0, "0:00:00")]
	[InlineData(45, "0:00:45")]
	[InlineData(60, "0:01:00")]
	[InlineData(125, "0:02:05")]
	[InlineData(3600, "1:00:00")]
	[InlineData(3661, "1:01:01")]
	[InlineData(5400, "1:30:00")]
	public void Format_shows_h_mm_ss(int elapsedSeconds, string expected)
	{
		var start = new DateTimeOffset(2025, 6, 14, 10, 0, 0, TimeSpan.Zero);
		var end = start.AddSeconds(elapsedSeconds);
		Assert.Equal(expected, ChecklistDurationFormatter.Format(start, end));
	}

	[Fact]
	public void Format_returns_dash_without_end()
	{
		var start = DateTimeOffset.Now.AddHours(-1);
		Assert.Equal("—", ChecklistDurationFormatter.Format(start, null));
		Assert.Equal("—", ChecklistDurationFormatter.Format(null, DateTimeOffset.Now));
	}

	[Fact]
	public void FormatActive_uses_now_for_live_counter()
	{
		var start = DateTimeOffset.Now.AddSeconds(-30);
		var formatted = ChecklistDurationFormatter.FormatActive(start, start.AddSeconds(30));
		Assert.Equal("0:00:30", formatted);
	}
}
