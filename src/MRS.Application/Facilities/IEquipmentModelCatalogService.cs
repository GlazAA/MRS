namespace MRS.Application.Facilities;

public sealed record EquipmentModelListItem(int Id, string Manufacturer, string Name);

public sealed record InstallationEquipmentModelInfo(
	int InstallationId,
	string? Manufacturer,
	string? ModelName,
	int? EquipmentModelId);

/// <summary>
/// Справочник производителей и моделей — общий по всей БД (не привязан к объекту/компании).
/// При записи пара сохраняется с типом оборудования; при чтении подсказки объединяются по всем типам,
/// чтобы одни и те же бренды/модели были доступны при создании новых сущностей.
/// </summary>
public interface IEquipmentModelCatalogService
{
	/// <summary>
	/// Производители из справочника (и ответов КЛ).
	/// <paramref name="equipmentTypeId"/> используется как подсказка приоритета; список общий.
	/// </summary>
	Task<IReadOnlyList<string>> GetManufacturersAsync(int equipmentTypeId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Модели производителя из справочника (и ответов КЛ). Список общий по всем типам;
	/// при совпадении имён предпочтение у строки с <paramref name="equipmentTypeId"/>.
	/// </summary>
	Task<IReadOnlyList<EquipmentModelListItem>> GetModelsAsync(
		int equipmentTypeId,
		string manufacturer,
		CancellationToken cancellationToken = default);

	Task<bool> HasAnyModelsAsync(int equipmentTypeId, CancellationToken cancellationToken = default);

	Task<int> EnsureModelAsync(
		int equipmentTypeId,
		string manufacturer,
		string modelName,
		CancellationToken cancellationToken = default);

	/// <summary>Найти или создать пару производитель+модель без дубликатов (без учёта регистра).</summary>
	Task<EquipmentModelListItem> EnsureModelEntryAsync(
		int equipmentTypeId,
		string manufacturer,
		string modelName,
		CancellationToken cancellationToken = default);

	/// <summary>Зафиксировать производителя в справочнике даже без модели (пустая name).</summary>
	Task EnsureManufacturerAsync(
		int equipmentTypeId,
		string manufacturer,
		CancellationToken cancellationToken = default);

	Task<InstallationEquipmentModelInfo?> GetInstallationModelAsync(
		int installationId,
		CancellationToken cancellationToken = default);
}
