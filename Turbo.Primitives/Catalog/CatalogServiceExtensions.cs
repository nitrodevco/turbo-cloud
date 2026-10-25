using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Catalog;

public static class CatalogServiceExtensions
{
    /// <summary>
    /// The catalog of this type as the player sees it: what serves a player (the index, a page, an
    /// offer, a purchase) asks this rather than <see cref="ICatalogService.GetCatalogSnapshot"/>,
    /// so a page they may not have is one they can neither open nor buy from. A hotel with no
    /// gated page pays nothing for it: the catalog is returned as it is.
    /// </summary>
    public static Task<CatalogSnapshot> GetCatalogSnapshotForPlayerAsync(
        this ICatalogService service,
        CatalogType catalogType,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var catalog = service.GetCatalogSnapshot(catalogType);

        return catalog.HasGatedPages
            ? service.CutForPlayerAsync(catalog, playerId, ct)
            : Task.FromResult(catalog);
    }
}
