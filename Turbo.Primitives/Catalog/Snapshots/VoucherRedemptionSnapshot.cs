using System;
using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>A player's redemption of a voucher.</summary>
[GenerateSerializer, Immutable]
public sealed record VoucherRedemptionSnapshot
{
    [Id(0)]
    public required int PlayerId { get; init; }

    [Id(1)]
    public required string PlayerName { get; init; }

    [Id(2)]
    public required DateTime RedeemedAt { get; init; }
}
