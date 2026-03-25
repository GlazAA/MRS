namespace MRS.Application.Checklists;

/// <summary>Типы оборудования, допустимые для выбранной системы объекта (<c>system_equipment_types</c>).</summary>
public interface IEquipmentTypeCatalogService
{
	Task<IReadOnlyList<EquipmentTypeListItem>> GetForSystemAsync(int facilitySystemId, CancellationToken cancellationToken = default);
}
