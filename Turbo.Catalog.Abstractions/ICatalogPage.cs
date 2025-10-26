using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog.Abstractions;

public interface ICatalogPage
{
    CatalogPageSnapshot Snapshot { get; }
    ICatalogPage? Parent { get; }
    void SetParent(ICatalogPage? parent);
    void AddChild(ICatalogPage child);
    void AddOffer(ICatalogOffer offer);
}
