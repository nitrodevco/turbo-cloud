namespace Turbo.Admin.Api.Contracts;

/// <summary>One page of the command log in <see cref="Order"/>.</summary>
public sealed record CommandLogResponse(
    int Total,
    int Page,
    int PageSize,
    ListOrder Order,
    CommandLogEntry[] Entries
);
