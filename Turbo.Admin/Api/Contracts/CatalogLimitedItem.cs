using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A limited series an offer sells: how many there are and are left, how long its raffle gathers
/// buyers, when it is on sale, whether it is, and whether its opening raffle has been drawn.
/// </summary>
public sealed record CatalogLimitedItem(
    int Id,
    int Total,
    int Remaining,
    int RaffleWindowSeconds,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    bool Active,
    bool RaffleFinished
);
