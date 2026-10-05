using System;

namespace Turbo.Primitives.Catalog.Editing;

/// <summary>
/// A limited series as an editor sets it: how many there are, how long the opening raffle
/// gathers buyers (0 sells first come, first served), the window it is on sale in (either end
/// open), and whether it is on sale at all. What is left of it is the hotel's to count.
/// </summary>
public sealed record CatalogLimitedDraft(
    int TotalQuantity,
    int RaffleWindowSeconds,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    bool Active
);
