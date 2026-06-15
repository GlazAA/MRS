using System.Globalization;

namespace MRS.Infrastructure.Sqlite;

internal static class SqliteDateTimeParsing
{
	internal static bool TryParseStored(string? raw, out DateTimeOffset dto)
	{
		dto = default;
		if (string.IsNullOrWhiteSpace(raw))
			return false;

		return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dto)
			|| DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dto)
			|| DateTimeOffset.TryParse(raw, out dto);
	}
}
