using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.Catalog;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

public record CatalogPageMessageComposer : IComposer
{
    public required CatalogSnapshot Catalog { get; init; }
    public required FurnitureSnapshot Furniture { get; init; }
    public required int PageId { get; init; }
    public required int OfferId { get; init; }
    public required bool AcceptSeasonCurrencyAsCredits { get; init; }
    public required List<CatalogFrontPageItemSnapshot> FrontPageItems { get; init; }
}
