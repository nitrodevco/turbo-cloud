using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;

namespace Turbo.Primitives.Catalog;

public interface ICatalogService
{
    public CatalogSnapshot GetCatalogSnapshot(CatalogType catalogType);

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
