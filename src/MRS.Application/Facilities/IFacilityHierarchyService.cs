namespace MRS.Application.Facilities;

/// <summary>Справочники для экрана выбора заказчика / объекта / системы (оффлайн SQLite).</summary>
public interface IFacilityHierarchyService
{
	Task<IReadOnlyList<HierarchyOption>> GetOrganizationsAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<HierarchyOption>> GetFacilitiesAsync(int organizationId, CancellationToken cancellationToken = default);

	/// <summary>Объекты организации с подписью «город, улица, дом» для экрана контрольных листов.</summary>
	Task<IReadOnlyList<HierarchyOption>> GetFacilitiesWithAddressAsync(int organizationId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<HierarchyOption>> GetSystemsAsync(int facilityId, CancellationToken cancellationToken = default);

	/// <summary>Все активные объекты (имена) — для фильтров сборки акта / списков КЛ.</summary>
	Task<IReadOnlyList<string>> GetAllFacilityNamesAsync(CancellationToken cancellationToken = default);
}
