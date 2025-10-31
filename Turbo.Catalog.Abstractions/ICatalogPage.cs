using System.Collections.Generic;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog.Abstractions;

public interface ICatalogPage
{
    public CatalogPageSnapshot Snapshot { get; }
    public ICatalogPage? Parent { get; }
    public IDictionary<int, ICatalogPage> Children { get; }
    public IDictionary<int, ICatalogOffer> Offers { get; }
    void SetParent(ICatalogPage? parent);
    void AddChild(ICatalogPage child);
    void AddOffer(ICatalogOffer offer);
}
