namespace MRS.Application.Admin;

public sealed record SqlQueryResult(
    bool Success,
    string? Error,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string?>> Rows,
    int? RowsAffected,
    string? Message);
