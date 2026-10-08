using System;
using Orleans;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>
/// One featured item on the catalog's front page: its place, title and promo picture, what it
/// opens, and how long it has left. <see cref="ExpiresAt"/> is when it comes off, null for
/// never; <see cref="ExpiresInSeconds"/> is what a client is sent, worked out from it when the
/// page is sent (<see cref="CatalogSnapshot.FrontPageItemsFor"/>), and 0 shows no countdown.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CatalogFrontPageItemSnapshot
{
    [Id(0)]
    public required int Position { get; init; }

    [Id(1)]
    public required string ItemName { get; init; }

    [Id(2)]
    public required string ItemPromoImage { get; init; }

    [Id(3)]
    public required CatalogFrontPageItemType Type { get; init; }

    [Id(4)]
    public required string? CatalogPageLocation { get; init; }

    [Id(5)]
    public required int? ProductOfferId { get; init; }

    [Id(6)]
    public required string? ProductCode { get; init; }

    [Id(7)]
    public required int ExpiresInSeconds { get; init; }

    [Id(8)]
    public DateTime? ExpiresAt { get; init; }
}
