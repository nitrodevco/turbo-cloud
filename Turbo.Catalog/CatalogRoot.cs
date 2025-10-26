using Turbo.Catalog.Abstractions;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog;

public class CatalogRoot : CatalogPage
{
    public CatalogRoot()
        : base(new CatalogPageSnapshot(-1, -1, string.Empty, "root", 0, "", [], [], true)) { }

    public override void SetParent(ICatalogPage? parent)
    {
        return;
    }
}
