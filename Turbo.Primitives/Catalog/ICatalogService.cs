using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Catalog;

public interface ICatalogService
{
    public CatalogSnapshot GetCatalogSnapshot(CatalogType catalogType);

    /// <summary>
    /// <paramref name="catalog"/> as the player sees it (<see cref="CatalogPageAccess"/>): the pages
    /// above their rank or needing a node they lack left out, with what is under them. Callers go
    /// through <see cref="CatalogServiceExtensions.GetCatalogSnapshotForPlayerAsync"/>, which skips
    /// this for a catalog that has no such page.
    /// </summary>
    public Task<CatalogSnapshot> CutForPlayerAsync(
        CatalogSnapshot catalog,
        PlayerId playerId,
        CancellationToken ct
    );

    /// <summary>
    /// A limited series sold one: the normal catalog's page and product offer say how many are
    /// left from now on (the limited item widget asks again every 20 seconds).
    /// </summary>
    public void SetLtdRemaining(int seriesId, int remaining);

    /// <summary>
    /// The Habbo Club memberships on sale, cheapest first. The buyer-dependent fields are left
    /// at zero; the caller fills them with <see cref="Snapshots.ClubOfferSnapshot.ForSubscription"/>.
    /// </summary>
    public ImmutableArray<ClubOfferSnapshot> GetClubOffers();

    /// <summary>
    /// The membership the club centre offers as a renewal: the longest whole-period one on sale,
    /// priced against the hotel's shortest offer so the saving it shows is real. Null when the
    /// hotel sells no membership of a whole period. The buyer-dependent fields are left at zero.
    /// </summary>
    public ClubExtendOfferSnapshot? GetClubExtendOffer();

    /// <summary>
    /// The offers a Habbo Club member may pick as a gift, the ones the shelf is drawn from,
    /// least demanding first. Empty for a hotel that offers no gifts.
    /// </summary>
    public ImmutableArray<CatalogOfferSnapshot> GetClubGiftOffers();

    public Task<UpcomingLtdSnapshot?> GetUpcomingLtdAsync(CancellationToken ct);
}
