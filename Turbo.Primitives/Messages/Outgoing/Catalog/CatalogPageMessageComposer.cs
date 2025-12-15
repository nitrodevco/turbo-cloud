using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record CatalogPageMessageComposer : IComposer
{
    [Id(0)]
    public required CatalogType CatalogType { get; init; }

    [Id(1)]
    public required CatalogPageSnapshot Page { get; init; }

    [Id(2)]
    public required ImmutableArray<CatalogOfferSnapshot> Offers { get; init; }

    [Id(3)]
    public required ImmutableDictionary<
        int,
        ImmutableArray<CatalogProductSnapshot>
    > OfferProducts { get; init; }

    [Id(4)]
    public required int OfferId { get; init; }

    [Id(5)]
    public required bool AcceptSeasonCurrencyAsCredits { get; init; }

    [Id(6)]
    public required ImmutableArray<CatalogFrontPageItemSnapshot> FrontPageItems { get; init; }
}
