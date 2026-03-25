namespace MRS.Application.Facilities;

public sealed record InstallationListItem(int Id, string Label);

/// <summary>Установки на выбранной системе для типа оборудования.</summary>
public interface IInstallationQueryService
{
	Task<IReadOnlyList<InstallationListItem>> GetForSystemAndEquipmentAsync(int facilitySystemId, int equipmentTypeId, CancellationToken cancellationToken = default);
}
