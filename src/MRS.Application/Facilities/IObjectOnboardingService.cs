namespace MRS.Application.Facilities;

/// <summary>
/// Сервис для "полевого" добавления новой сущности в иерархию:
/// организация -> объект -> система -> тип оборудования -> установка.
/// </summary>
public interface IObjectOnboardingService
{
    Task<IReadOnlyList<HierarchyOption>> GetAllEquipmentTypesAsync(CancellationToken cancellationToken = default);

    Task<ObjectOnboardingResult> UpsertHierarchyAsync(
        ObjectOnboardingRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ObjectOnboardingRequest(
    int? ExistingOrganizationId,
    string? NewOrganizationFullName,
    string? NewOrganizationShortName,
    int? ExistingFacilityId,
    string? NewFacilityName,
    string? AddressCity,
    string? AddressStreet,
    string? AddressBuilding,
    string? AddressRegion,
    string? AddressZipCode,
    int? ExistingSystemId,
    string? NewSystemName,
    string? NewSystemDescription,
    int? ExistingEquipmentTypeId,
    string? NewEquipmentTypeName,
    string? NewEquipmentTypeCode,
    string InstallationLabel,
    string? InstallationModel,
    string? InstallationSerialNumber);

public sealed record ObjectOnboardingResult(
    int OrganizationId,
    int FacilityId,
    int SystemId,
    int EquipmentTypeId,
    int InstallationId,
    bool OrganizationCreated,
    bool FacilityCreated,
    bool SystemCreated,
    bool EquipmentTypeCreated,
    bool InstallationCreated);
