namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A page of the sanctions log in <see cref="Order"/>, counted no further than the other logs are
/// (<see cref="TotalCapped"/> when there are more than <see cref="Total"/>).
/// </summary>
public sealed record SanctionLogResponse(
    int Total,
    bool TotalCapped,
    int Page,
    int PageSize,
    ListOrder Order,
    SanctionLogEntry[] Entries
);
