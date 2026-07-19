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

	/// <summary>
	/// Название для списков в приложении: только имя компании, без «ООО»/«ЗАО».
	/// Юр. форма показывается в документах через <see cref="FormatActName"/>.
	/// </summary>
	public static string FormatListName(string? legalFormCode, string fullName, string? shortName)
	{
		_ = legalFormCode;
		var company = (fullName ?? string.Empty).Trim();
		if (company.Length > 0)
			return StripLeadingLegalShortLabel(company);

		return (shortName ?? string.Empty).Trim();
	}

	/// <summary>Полное наименование для акта: юр. форма словами + название без кавычек.</summary>
	public static string FormatActName(string? legalFormCode, string fullName, string? shortName)
	{
		var company = (fullName ?? string.Empty).Trim();
		if (company.Length > 0)
			company = StripLeadingLegalShortLabel(company);

		if (TryGet(legalFormCode) is { } form && company.Length > 0)
			return $"{form.FullLabel} {company}";

		var legacy = (shortName ?? string.Empty).Trim();
		if (legacy.Length > 0)
			return legacy;

		return company;
	}

	/// <summary>Убирает случайно введённый короткий префикс («ООО », «ЗАО », лат. OOO) из названия.</summary>
	public static string StripLeadingLegalShortLabel(string companyName)
	{
		var name = NormalizeSpaces(companyName);
		if (name.Length == 0)
			return name;

		// Повторяем: «ООО ООО Рога» → «Рога».
		for (var guard = 0; guard < 4; guard++)
		{
			var stripped = TryStripOneLeadingLegalPrefix(name);
			if (stripped is null)
				break;
			name = stripped;
		}

		return name;
	}

	private static string? TryStripOneLeadingLegalPrefix(string name)
	{
		foreach (var form in Choices.OrderByDescending(c => c.ShortLabel.Length))
		{
			foreach (var label in LegalPrefixVariants(form))
			{
				if (name.Length <= label.Length)
					continue;
				if (!name.StartsWith(label, StringComparison.OrdinalIgnoreCase))
					continue;
				var rest = name[label.Length..];
				if (rest.Length == 0)
					continue;
				// «ООО Рога», «ООО"Рога"», «ООО«Рога»»
				var next = rest[0];
				if (char.IsWhiteSpace(next) || next is '"' or '«' or '„')
					return rest.TrimStart().Trim('"', '«', '»', '„', '“', '”').Trim();
			}
		}

		return null;
	}

	private static IEnumerable<string> LegalPrefixVariants(Option form)
	{
		yield return form.ShortLabel;
		// На клавиатуре часто набирают латиницей код формы вместо кириллической подписи.
		yield return form.Code switch
		{
			"OOO" => "OOO",
			"ZAO" => "ZAO",
			"AO" => "AO",
			"IP" => "IP",
			_ => form.Code
		};
	}

	private static string NormalizeSpaces(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;
		return string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
	}
}
