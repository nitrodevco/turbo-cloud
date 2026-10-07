using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Catalog.Tags;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// The offer stamps of the catalogs players see now (<see cref="FurnitureOfferStamps"/>). A
/// catalog snapshot is replaced whole when the catalog is published, so the stamps are worked out
/// again only when either snapshot is a new one.
/// </summary>
internal sealed class FurnitureOfferCatalog(
    ICatalogSnapshotProvider<NormalCatalog> normalCatalog,
    ICatalogSnapshotProvider<BuildersClubCatalog> buildersClubCatalog
)
{
    private Stamps? _stamps;

    public async Task<Stamps> GetAsync(CancellationToken ct)
    {
        var normal = await normalCatalog.GetSnapshotAsync(ct).ConfigureAwait(false);
        var buildersClub = await buildersClubCatalog.GetSnapshotAsync(ct).ConfigureAwait(false);

        if (
            _stamps is { } known
            && ReferenceEquals(known.Normal, normal)
            && ReferenceEquals(known.BuildersClub, buildersClub)
        )
            return known;

        var stamps = new Stamps(
            normal,
            buildersClub,
            FurnitureOfferStamps.Of(normal),
            FurnitureOfferStamps.Of(buildersClub)
        );

        _stamps = stamps;

        return stamps;
    }

    /// <summary>The stamps, and the snapshots they were worked out from.</summary>
    public sealed record Stamps(
        CatalogSnapshot Normal,
        CatalogSnapshot BuildersClub,
        IReadOnlyDictionary<int, int> Offers,
        IReadOnlyDictionary<int, int> BuildersClubOffers
    );
}
