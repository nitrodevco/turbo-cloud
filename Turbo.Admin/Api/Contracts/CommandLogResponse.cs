namespace Turbo.Admin.Api.Contracts;

/// <summary>One page of the command log, newest first.</summary>
public sealed record CommandLogResponse(
    int Total,
    int Page,
    int PageSize,
    CommandLogEntry[] Entries
);
