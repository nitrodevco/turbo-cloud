using System.Collections.Generic;
using Turbo.Catalog.Abstractions;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog;

public class CatalogPage(CatalogPageSnapshot pageSnapshot) : ICatalogPage
{
    public CatalogPageSnapshot Snapshot => pageSnapshot;
    public ICatalogPage? Parent { get; private set; }
    public IDictionary<int, ICatalogPage> Children { get; } = new Dictionary<int, ICatalogPage>();
    public IDictionary<int, ICatalogOffer> Offers { get; } = new Dictionary<int, ICatalogOffer>();

    public virtual void SetParent(ICatalogPage? parent)
    {
        if (Parent == parent)
            return;

        Parent = parent;

        Parent?.AddChild(this);
    }

    public void AddChild(ICatalogPage child)
    {
        if (Children.ContainsKey(child.Snapshot.Id))
            return;

        Children.Add(child.Snapshot.Id, child);

        child.SetParent(this);
    }

    public void AddOffer(ICatalogOffer offer)
    {
        if (Offers.ContainsKey(offer.Snapshot.Id))
            return;

        Offers.Add(offer.Snapshot.Id, offer);

        offer.SetPage(this);
    }
}
