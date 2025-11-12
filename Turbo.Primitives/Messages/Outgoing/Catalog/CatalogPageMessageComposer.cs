using System.Collections.Generic;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Catalog;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record CatalogPageMessageComposer : IComposer
{
    [Id(0)]
    public required CatalogTypeEnum CatalogType { get; init; }

    [Id(1)]
    public required CatalogPageSnapshot Page { get; init; }

    [Id(2)]
    public required List<CatalogOfferSnapshot> Offers { get; init; }

    [Id(3)]
    public required Dictionary<int, List<CatalogProductSnapshot>> OfferProducts { get; init; }

    [Id(4)]
    public required int OfferId { get; init; }

    [Id(5)]
    public required bool AcceptSeasonCurrencyAsCredits { get; init; }

    [Id(6)]
    public required List<CatalogFrontPageItemSnapshot> FrontPageItems { get; init; }
}
