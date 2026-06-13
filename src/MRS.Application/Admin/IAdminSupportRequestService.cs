namespace MRS.Application.Admin;

public interface IAdminSupportRequestService
{
    Task<int> SubmitAsync(int? authorUserId, string authorDisplayName, string body, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminSupportRequest>> ListAsync(CancellationToken cancellationToken = default);

    Task ResolveAsync(int requestId, string? adminReply, CancellationToken cancellationToken = default);
}
