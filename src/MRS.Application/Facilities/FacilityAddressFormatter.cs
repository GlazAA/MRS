namespace MRS.Application.Facilities;

/// <summary>Форматирование адреса объекта для списков выбора.</summary>
public static class FacilityAddressFormatter
{
	public static string Format(string city, string street, string building, string? structure, string? block)
	{
		var parts = new List<string>();
		var cityPart = (city ?? string.Empty).Trim();
		var streetPart = (street ?? string.Empty).Trim();
		var housePart = FormatHouse(building, structure, block);

		if (cityPart.Length > 0)
			parts.Add(cityPart);
		if (streetPart.Length > 0)
			parts.Add(streetPart);
		if (housePart.Length > 0)
			parts.Add(housePart);

		return parts.Count == 0 ? "—" : string.Join(", ", parts);
	}

	/// <summary>Строка «Объект» для акта: город и полный адрес в скобках.</summary>
	public static string FormatActObject(string? city, string street, string building, string? structure, string? block)
	{
		var cityPart = (city ?? string.Empty).Trim();
		var fullAddress = Format(city ?? string.Empty, street, building, structure, block);
		if (cityPart.Length == 0)
			return fullAddress;
		if (fullAddress.Length == 0)
			return cityPart;
		return $"{cityPart}\n({fullAddress})";
	}

	private static string FormatHouse(string building, string? structure, string? block)
	{
		var house = (building ?? string.Empty).Trim();
		if (house.Length == 0)
			return string.Empty;

		var structurePart = (structure ?? string.Empty).Trim();
		var blockPart = (block ?? string.Empty).Trim();
		if (structurePart.Length > 0)
			house += $" стр. {structurePart}";
		if (blockPart.Length > 0)
			house += $" корп. {blockPart}";

		return house;
	}
}
