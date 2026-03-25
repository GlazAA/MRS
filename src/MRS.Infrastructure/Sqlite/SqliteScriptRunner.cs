using System.Text;

namespace MRS.Infrastructure.Sqlite;

internal static class SqliteScriptRunner
{
	internal static IReadOnlyList<string> SplitStatements(string sql)
	{
		var sb = new StringBuilder();
		using var reader = new StringReader(sql);
		string? line;
		while ((line = reader.ReadLine()) != null)
		{
			var trimmed = line.TrimStart();
			if (trimmed.StartsWith("--", StringComparison.Ordinal))
				continue;
			sb.AppendLine(line);
		}

		var body = sb.ToString();
		var parts = body.Split(new[] { ";\r\n", ";\n", ";\r" }, StringSplitOptions.RemoveEmptyEntries);
		var list = new List<string>(parts.Length);
		foreach (var part in parts)
		{
			var s = part.Trim();
			if (s.Length > 0)
				list.Add(s);
		}

		return list;
	}

	internal static async Task ExecuteScriptAsync(
		Microsoft.Data.Sqlite.SqliteConnection connection,
		string sql,
		CancellationToken cancellationToken)
	{
		foreach (var statement in SplitStatements(sql))
		{
			cancellationToken.ThrowIfCancellationRequested();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = statement;
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
	}
}
