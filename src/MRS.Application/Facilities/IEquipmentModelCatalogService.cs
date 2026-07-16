namespace MRS.Application.Facilities;

public sealed record EquipmentModelListItem(int Id, string Manufacturer, string Name);

public sealed record InstallationEquipmentModelInfo(
	int InstallationId,
	string? Manufacturer,
	string? ModelName,
	int? EquipmentModelId);

/// <summary>Справочник производителей и моделей по типу оборудования (общий, не привязан к объекту/компании).</summary>
public interface IEquipmentModelCatalogService
{
	Task<IReadOnlyList<string>> GetManufacturersAsync(int equipmentTypeId, CancellationToken cancellationToken = default);

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

	Task<InstallationEquipmentModelInfo?> GetInstallationModelAsync(
		int installationId,
		CancellationToken cancellationToken = default);
}
