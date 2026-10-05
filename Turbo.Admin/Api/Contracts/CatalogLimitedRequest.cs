using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A limited series as the editor saves it; what is left is the hotel's to count.</summary>
public sealed record CatalogLimitedRequest(
    int TotalQuantity,
    int RaffleWindowSeconds,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    bool Active
);
