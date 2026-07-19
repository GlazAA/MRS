namespace MRS.Application.Facilities;

/// <summary>
/// Сервис для "полевого" добавления новой сущности в иерархию:
/// организация -> объект -> система -> тип оборудования -> установка (+ контакты объекта).
/// </summary>
public interface IObjectOnboardingService
{
    Task<IReadOnlyList<HierarchyOption>> GetAllEquipmentTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>Уникальные должности из всех контактов (organization_employees) — для подсказок.</summary>
    Task<IReadOnlyList<string>> GetDistinctContactPositionsAsync(CancellationToken cancellationToken = default);

    Task<ObjectOnboardingResult> UpsertHierarchyAsync(
        ObjectOnboardingRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ObjectOnboardingInstallationDraft(
    int? ExistingEquipmentTypeId,
    string? NewEquipmentTypeName,
    string InstallationLabel,
    string? InstallationManufacturer,
    string? InstallationModel,
    string? InstallationSerialNumber);

/// <summary>Контакт заказчика: ФИО отдельными полями (как в organization_employees / users).</summary>
public sealed record ObjectOnboardingContactDraft(
    string LastName,
    string FirstName,
    string? MiddleName,
    string? Position,
    string? Phone,
    string? Email);

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
    IReadOnlyList<ObjectOnboardingInstallationDraft> Installations,
    IReadOnlyList<ObjectOnboardingContactDraft> Contacts);

public sealed record ObjectOnboardingResult(
    int OrganizationId,
    int FacilityId,
    int SystemId,
    int PrimaryEquipmentTypeId,
    int PrimaryInstallationId,
    int InstallationsSaved,
    int ContactsSaved,
    bool OrganizationCreated,
    bool FacilityCreated,
    bool SystemCreated);
