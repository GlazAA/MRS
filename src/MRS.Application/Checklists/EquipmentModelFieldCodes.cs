namespace MRS.Application.Checklists;

public static class EquipmentModelFieldCodes
{
	public const string ManufacturerSuffix = "_manufacturer";
	public const string ModelSuffix = "_model";

	/// <summary>
	/// Префикс поля шаблона → id типа оборудования из справочника (seed).
	/// Нужно для «Единого ТО», где в одном листе поля разных устройств.
	/// </summary>
	private static readonly Dictionary<string, int> PrefixToEquipmentTypeId =
		new(StringComparer.OrdinalIgnoreCase)
		{
			["comp"] = 1,              // Винтовой компрессор
			["motor"] = 2,             // Электродвигатель / ПЭД
			["oht"] = 3,               // Осушитель холодильного типа
			["cyclone"] = 4,           // Циклонный сепаратор
			["filter"] = 5,            // Фильтры очистки
			["ads"] = 6,               // Угольный адсорбер
			["cond"] = 7,              // Конденсатоотводчики
			["wms"] = 8,               // Водомасляный сепаратор
			["recv"] = 9,              // Ресиверы
			["grm"] = 10,              // Газоразделительный модуль
			["cshu"] = 11,             // Центральный шкаф управления
			["cshu_battery"] = 11,
			["shuzz"] = 12,            // Шкаф управления зоной защиты
			["shuzz_battery"] = 12,
			["dcm"] = 13               // Датчики, контроллеры и модули
		};

	public static bool IsManufacturerField(string? fieldCode) =>
		fieldCode is not null &&
		fieldCode.EndsWith(ManufacturerSuffix, StringComparison.OrdinalIgnoreCase);

	public static bool IsModelField(string? fieldCode) =>
		fieldCode is not null &&
		fieldCode.EndsWith(ModelSuffix, StringComparison.OrdinalIgnoreCase) &&
		!IsManufacturerField(fieldCode);

	public static string? GetPrefix(string? fieldCode)
	{
		if (string.IsNullOrWhiteSpace(fieldCode))
			return null;

		if (fieldCode.EndsWith(ManufacturerSuffix, StringComparison.OrdinalIgnoreCase))
			return fieldCode[..^ManufacturerSuffix.Length];

		if (fieldCode.EndsWith(ModelSuffix, StringComparison.OrdinalIgnoreCase))
			return fieldCode[..^ModelSuffix.Length];

		return null;
	}

	public static string? GetManufacturerFieldCode(string? modelFieldCode)
	{
		var prefix = GetPrefix(modelFieldCode);
		return prefix is null ? null : prefix + ManufacturerSuffix;
	}

	public static string? GetModelFieldCode(string? manufacturerFieldCode)
	{
		var prefix = GetPrefix(manufacturerFieldCode);
		return prefix is null ? null : prefix + ModelSuffix;
	}

	/// <summary>
	/// Тип оборудования для справочника производитель/модель по коду поля.
	/// Если префикс неизвестен — тип установки (fallbackInstallationEquipmentTypeId).
	/// </summary>
	public static int ResolveCatalogEquipmentTypeId(string? fieldCode, int fallbackInstallationEquipmentTypeId)
	{
		var prefix = GetPrefix(fieldCode);
		if (prefix is null)
			return fallbackInstallationEquipmentTypeId;

		if (PrefixToEquipmentTypeId.TryGetValue(prefix, out var mapped))
			return mapped;

		return fallbackInstallationEquipmentTypeId;
	}
}
