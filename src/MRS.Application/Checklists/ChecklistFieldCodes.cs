namespace MRS.Application.Checklists;

public static class ChecklistFieldCodes
{
	public const string EndDate = "end_date";
	public const string EndTime = "end_time";
	public const string Workers = "workers";
	public const string UnitNumber = "unit_number";
	public const string UnitNo = "unit_no";
	public const string EquipmentPick = "equipment_pick";

	public static bool IsEndTime(string? fieldCode) =>
		fieldCode is not null && fieldCode.Equals(EndTime, StringComparison.OrdinalIgnoreCase);

	public static bool IsWorkers(string? fieldCode) =>
		fieldCode is not null && fieldCode.Equals(Workers, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Номер установки: дублирует справочник installations — на форме заполнения не показываем,
	/// значение пишется из блока «Установка» (выбор / создание).
	/// </summary>
	public static bool IsUnitNumber(string? fieldCode) =>
		fieldCode is not null && (
			fieldCode.Equals(UnitNumber, StringComparison.OrdinalIgnoreCase) ||
			fieldCode.Equals(UnitNo, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Тип оборудования из шаблона Mosarchive: дублирует выбор на экране до КЛ.
	/// На форме скрыт, в ответ пишется имя типа из маршрута / установки.
	/// </summary>
	public static bool IsEquipmentPick(string? fieldCode) =>
		fieldCode is not null && fieldCode.Equals(EquipmentPick, StringComparison.OrdinalIgnoreCase);

	/// <summary>Поля шаблона, которые не рендерим при заполнении / правке КЛ.</summary>
	public static bool IsHiddenOnFillForm(string? fieldCode) =>
		IsEndTime(fieldCode) || IsUnitNumber(fieldCode) || IsEquipmentPick(fieldCode);
}
