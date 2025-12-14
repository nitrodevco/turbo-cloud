using Orleans;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Primitives.Catalog.Snapshots;

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
}
