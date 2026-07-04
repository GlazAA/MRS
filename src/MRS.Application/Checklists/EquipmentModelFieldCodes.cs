namespace MRS.Application.Checklists;

public static class EquipmentModelFieldCodes
{
	public const string ManufacturerSuffix = "_manufacturer";
	public const string ModelSuffix = "_model";

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
}
