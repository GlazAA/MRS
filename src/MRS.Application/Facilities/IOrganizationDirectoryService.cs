namespace MRS.Application.Facilities;

public sealed record OrganizationOverviewItem(
	int Id,
	string Name,
	string? LegalFormCode,
	string? LegalFormLabel,
	int FacilityCount,
	int ContactCount,
	string? LastVisitEngineerName,
	DateTimeOffset? LastVisitAt);

public sealed record OrganizationFacilityBrief(
	int Id,
	string Name,
	string? AddressLabel,
	string? SystemDescription);

public sealed record OrganizationContactBrief(
	int Id,
	int? FacilityId,
	string? FacilityName,
	string LastName,
	string FirstName,
	string? MiddleName,
	string DisplayName,
	string? Position,
	string? Phone,
	string? Email);

public sealed record OrganizationDetail(
	int Id,
	string Name,
	string? LegalFormCode,
	string? LegalFormLabel,
	IReadOnlyList<OrganizationFacilityBrief> Facilities,
	IReadOnlyList<OrganizationContactBrief> Contacts,
	string? LastVisitEngineerName,
	DateTimeOffset? LastVisitAt,
	string? LastVisitFacilityName);

public interface IOrganizationDirectoryService
{
	Task<IReadOnlyList<OrganizationOverviewItem>> ListAsync(
		string? query = null,
		CancellationToken cancellationToken = default);

	Task<OrganizationDetail?> GetAsync(int organizationId, CancellationToken cancellationToken = default);
}
