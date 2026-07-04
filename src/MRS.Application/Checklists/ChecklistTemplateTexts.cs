namespace MRS.Application.Checklists;

/// <summary>
/// Канонические тексты верхней плашки и окна безопасности контрольных листов.
/// </summary>
public static class ChecklistTemplateTexts
{
	public const string DefaultTopPlateText =
		"При наличии ошибок и неисправностей следует соблюдать инструкции и рекомендации, указанные в руководстве по эксплуатации.";

	public const string To1500TopPlateSuffix = "Внимание! Обслуживание выполняется с ТО-1500.";

	public static string AnnualTopPlateText => $"{DefaultTopPlateText}\n{To1500TopPlateSuffix}";

	public const string MotorTopPlateText =
		"ТО - 1400. При наличии ошибок и неисправностей следует соблюдать инструкции и рекомендации, указанные в руководстве по эксплуатации.";

	public const string FilterTopPlateText = "Замена фильтрующих элементов не реже 1 раза в год.";

	public const string AdsorberTopPlateText =
		"Адсорберы на основе активированного угля. При наличии ошибок и неисправностей следует соблюдать инструкции и рекомендации, указанные в руководстве по эксплуатации.";

	public const string ReceiverTopPlateText = "Еженедельное техническое обслуживание.";

	public const string DcmTopPlateText =
		"Устройства имеют систему самодиагностики и постоянно проверяются в повседневной эксплуатации. Проверка устройств производится в случае сообщений о неисправности. Замена компонентов производится согласно регламенту.";

	public const string DefaultSafetyModalText =
		"ВНИМАНИЕ! Перед всеми работами по техническому обслуживанию:\n" +
		"1. Отключить компрессор при помощи кнопки ВЫКЛ.\n" +
		"2. Привести в действие переключатель аварийного останова.\n" +
		"3. Разомкнуть устройство отключения от сети и обезопасить с помощью висячего замка от непреднамеренного повторного включения.\n" +
		"4. Разместить на устройстве управления предупреждающую табличку.\n" +
		"5. Проверить, действительно ли обесточены все детали установки.\n" +
		"6. Перед началом работы дать всем горячим элементам конструкции компрессора остыть до 50°C.\n" +
		"7. Отсоединить компрессор от сети сжатого воздуха.\n" +
		"Для этого закрыть шаровой кран на выходе сжатого воздуха.\n" +
		"9. Удалить воздух из системы компрессора.";

	public static IReadOnlyList<ChecklistTemplateTextPreset> TopPlatePresets { get; } =
	[
		new("Стандартная фраза", DefaultTopPlateText),
		new("ТО-1500", AnnualTopPlateText),
		new("ТО-1400 + стандарт", MotorTopPlateText),
		new("Фильтры", FilterTopPlateText),
		new("Адсорберы", AdsorberTopPlateText),
		new("Ресиверы (еженед.)", ReceiverTopPlateText),
		new("ДКМ / самодиагностика", DcmTopPlateText),
	];

	public static IReadOnlyList<ChecklistTemplateTextPreset> SafetyModalPresets { get; } =
	[
		new("ВНИМАНИЕ (стандарт)", DefaultSafetyModalText),
	];
}

public sealed record ChecklistTemplateTextPreset(string Label, string Text);
