namespace MRS.Application.Facilities;

/// <summary>Юридические формы организаций для полевого ввода и акта.</summary>
public static class OrganizationLegalForm
{
	public sealed record Option(string Code, string ShortLabel, string FullLabel);

	public static IReadOnlyList<Option> Choices { get; } =
	[
		new("OOO", "ООО", "Общество с ограниченной ответственностью"),
		new("IP", "ИП", "Индивидуальный предприниматель"),
		new("SELF_EMPLOYED", "самозанятый", "Самозанятый"),
		new("AO", "АО", "Акционерное общество"),
		new("ZAO", "ЗАО", "Закрытое акционерное общество")
	];

	private static readonly Dictionary<string, Option> ByCode =
		Choices.ToDictionary(o => o.Code, StringComparer.OrdinalIgnoreCase);

	public static bool IsValidCode(string? code) =>
		code is not null && ByCode.ContainsKey(code);

	public static Option? TryGet(string? code) =>
		code is not null && ByCode.TryGetValue(code, out var option) ? option : null;

	public static string FormatListName(string? legalFormCode, string fullName, string? shortName)
	{
		var company = (fullName ?? string.Empty).Trim();
		if (TryGet(legalFormCode) is { } form && company.Length > 0)
			return $"{form.ShortLabel} {company}";

		var legacy = (shortName ?? string.Empty).Trim();
		if (legacy.Length > 0)
			return legacy;

		return company;
	}

	/// <summary>Полное наименование для акта: юр. форма словами + название без кавычек.</summary>
	public static string FormatActName(string? legalFormCode, string fullName, string? shortName)
	{
		var company = (fullName ?? string.Empty).Trim();
		if (TryGet(legalFormCode) is { } form && company.Length > 0)
			return $"{form.FullLabel} {company}";

		var legacy = (shortName ?? string.Empty).Trim();
		if (legacy.Length > 0)
			return legacy;

		return company;
	}
}
