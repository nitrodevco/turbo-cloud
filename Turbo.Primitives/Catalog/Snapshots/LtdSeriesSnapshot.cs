using System;
using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>
/// Snapshot of an LTD (Limited Edition) series configuration.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record LtdSeriesSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required int CatalogProductId { get; init; }

    [Id(3)]
    public required int TotalQuantity { get; init; }

    [Id(4)]
    public required int RemainingQuantity { get; init; }

    [Id(6)]
    public required int RaffleWindowSeconds { get; init; }

    [Id(7)]
    public required bool IsActive { get; init; }

    [Id(8)]
    public required bool IsRaffleFinished { get; init; }

    [Id(9)]
    public DateTime? StartsAt { get; init; }

    [Id(10)]
    public DateTime? EndsAt { get; init; }

    /// <summary>
    /// Whether this LTD series is currently available for purchase.
    /// </summary>
    public bool IsAvailable =>
        IsActive
        && RemainingQuantity > 0
        && (StartsAt == null || StartsAt <= DateTime.UtcNow)
        && (EndsAt == null || EndsAt >= DateTime.UtcNow);
}
