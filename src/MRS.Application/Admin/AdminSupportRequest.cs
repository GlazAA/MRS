namespace MRS.Application.Admin;

public sealed record AdminSupportRequest(
    int Id,
    int? AuthorUserId,
    string AuthorDisplayName,
    string Body,
    string Status,
    string? AdminReply,
    string CreatedAt,
    string? ResolvedAt);
