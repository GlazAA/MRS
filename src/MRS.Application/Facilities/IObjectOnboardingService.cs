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
    string? NewOrganizationLegalFormCode,
    string? NewOrganizationCompanyName,
    int? ExistingFacilityId,
    string? NewFacilityName,
    string? ContractAddress,
    string? AddressCity,
    string? AddressStreet,
    string? AddressBuilding,
    string? AddressStructure,
    string? AddressBlock,
    string? AddressZipCode,
    string? SystemDescription,
    int? ExistingEquipmentTypeId,
    string? NewEquipmentTypeName,
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
