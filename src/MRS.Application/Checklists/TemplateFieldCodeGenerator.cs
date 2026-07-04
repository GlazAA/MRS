using System.Globalization;
using System.Text;

namespace MRS.Application.Checklists;

/// <summary>Автоподбор служебных кодов при создании шаблона.</summary>
public static class TemplateFieldCodeGenerator
{
	private static readonly Dictionary<string, string> KnownQuestionCodes = new(StringComparer.OrdinalIgnoreCase)
	{
		["Дата начала"] = "start_date",
		["Время начала"] = "start_time",
		["Дата окончания"] = "end_date",
		["Время окончания"] = "end_time",
		["Номер установки"] = "unit_number",
		["Оборудование"] = "equipment_pick",
		["Лица, производившие работы"] = "workers",
		["Производитель компрессора"] = "comp_manufacturer",
		["Модель компрессора"] = "comp_model",
		["Состояние компрессора"] = "comp_state",
		["Серийный номер"] = "comp_serial",
	};

	private static readonly IReadOnlyDictionary<char, string> Cyrillic = new Dictionary<char, string>
	{
		['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d",
		['е'] = "e", ['ё'] = "e", ['ж'] = "zh", ['з'] = "z", ['и'] = "i",
		['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n",
		['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
		['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch",
		['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "",
		['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
	};

	public static string SuggestFromQuestion(string? question, IReadOnlyCollection<string> usedCodes)
	{
		var trimmed = (question ?? string.Empty).Trim();
		if (trimmed.Length == 0)
			return EnsureUnique("field", usedCodes);

		if (KnownQuestionCodes.TryGetValue(trimmed, out var known))
			return EnsureUnique(known, usedCodes);

		var slug = Slugify(trimmed);
		if (slug.Length == 0)
			slug = "field";

		return EnsureUnique(slug, usedCodes);
	}

	public static string SuggestMaintenanceTypeCode(string? typeName)
	{
		var slug = Slugify(typeName ?? string.Empty);
		if (slug.Length == 0)
			return "INT-CUSTOM";

		return $"INT-{slug.Replace('_', '-').ToUpperInvariant()}";
	}

	public static string SuggestScenarioCode(string? templateName, int equipmentTypeId)
	{
		var slug = Slugify(templateName ?? string.Empty);
		if (slug.Length == 0)
			slug = "template";

		return $"SC-ET{equipmentTypeId.ToString(CultureInfo.InvariantCulture)}-{slug.Replace('_', '-').ToUpperInvariant()}";
	}

	public static string EnsureUnique(string baseCode, IReadOnlyCollection<string> usedCodes)
	{
		var code = baseCode;
		var n = 2;
		while (usedCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
		{
			code = $"{baseCode}_{n.ToString(CultureInfo.InvariantCulture)}";
			n++;
		}

		return code;
	}

	public static string Slugify(string text)
	{
		var sb = new StringBuilder(text.Length * 2);
		var prevUnderscore = false;

		foreach (var ch in text.Trim().ToLowerInvariant())
		{
			string? part = null;
			if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
				part = ch.ToString();
			else if (Cyrillic.TryGetValue(ch, out var cyr))
				part = cyr;
			else if (ch is ' ' or '-' or '.' or ',' or '/' or '\\' or '(' or ')' or '—')
				part = "_";

			if (part is null)
				continue;

			foreach (var p in part)
			{
				if (p == '_')
				{
					if (!prevUnderscore && sb.Length > 0)
					{
						sb.Append('_');
						prevUnderscore = true;
					}

					continue;
				}

				sb.Append(p);
				prevUnderscore = false;
			}
		}

		return sb.ToString().Trim('_');
	}
}
