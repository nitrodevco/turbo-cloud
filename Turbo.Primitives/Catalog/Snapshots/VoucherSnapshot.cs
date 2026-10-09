using System;
using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>
/// A voucher: a code a player redeems once in the catalogue for its rewards - credits, another
/// currency, furniture, a badge - while it is turned on, before it expires and until it is used up.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record VoucherSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    /// <summary>Kept in capitals; a code is typed in any case.</summary>
    [Id(1)]
    public required string Code { get; init; }

    [Id(2)]
    public required int Credits { get; init; }

    /// <summary>Another currency it gives (a <c>currency_types</c> row), with <see cref="CurrencyAmount"/>.</summary>
    [Id(3)]
    public int? CurrencyTypeId { get; init; }

    [Id(4)]
    public required int CurrencyAmount { get; init; }

    /// <summary>Furniture it gives, with <see cref="FurnitureQuantity"/>.</summary>
    [Id(5)]
    public int? FurnitureDefinitionId { get; init; }

    [Id(6)]
    public required int FurnitureQuantity { get; init; }

    [Id(7)]
    public string? BadgeCode { get; init; }

    /// <summary>Redemptions it allows in all; null for any number (each player still redeems it once).</summary>
    [Id(8)]
    public int? MaxUses { get; init; }

    [Id(9)]
    public required int Uses { get; init; }

    /// <summary>When it stops being redeemable (UTC); null for never.</summary>
    [Id(10)]
    public DateTime? ExpiresAt { get; init; }

    [Id(11)]
    public required bool Enabled { get; init; }

    /// <summary>What staff wrote about it: who it was for, where it was given out.</summary>
    [Id(12)]
    public required string Note { get; init; }

    [Id(13)]
    public required DateTime CreatedAt { get; init; }
}
