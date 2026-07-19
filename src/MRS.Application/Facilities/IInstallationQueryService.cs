namespace MRS.Application.Facilities;

public sealed record InstallationListItem(int Id, string Label);

public sealed record InstallationFilterItem(string FacilityName, string Label);

/// <summary>Установки на выбранной системе для типа оборудования.</summary>
public interface IInstallationQueryService
{
	Task<IReadOnlyList<InstallationListItem>> GetForSystemAndEquipmentAsync(int facilitySystemId, int equipmentTypeId, CancellationToken cancellationToken = default);

	/// <summary>Все активные установки с именем объекта — для фильтров сборки акта.</summary>
	Task<IReadOnlyList<InstallationFilterItem>> ListActiveForFiltersAsync(CancellationToken cancellationToken = default);
}
