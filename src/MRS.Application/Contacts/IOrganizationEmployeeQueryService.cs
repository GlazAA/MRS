namespace MRS.Application.Contacts;

public sealed record OrganizationEmployeeOption(
	int Id,
	int OrganizationId,
	string DisplayName,
	string? Position,
	string? Phone,
	string? Email);

public interface IOrganizationEmployeeQueryService
{
	Task<IReadOnlyList<OrganizationEmployeeOption>> SearchAsync(int organizationId, string? query, CancellationToken cancellationToken = default);
}
