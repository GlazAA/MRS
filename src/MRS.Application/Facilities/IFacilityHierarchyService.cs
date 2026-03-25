namespace MRS.Application.Facilities;

/// <summary>Справочники для экрана выбора заказчика / объекта / системы (оффлайн SQLite).</summary>
public interface IFacilityHierarchyService
{
	Task<IReadOnlyList<HierarchyOption>> GetOrganizationsAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<HierarchyOption>> GetFacilitiesAsync(int organizationId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<HierarchyOption>> GetSystemsAsync(int facilityId, CancellationToken cancellationToken = default);
}
