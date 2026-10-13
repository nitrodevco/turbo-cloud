using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Catalog.Marketplace;

/// <summary>
/// The marketplace: players offer inventory furni for credits, others search the open offers and
/// buy them. An offered item leaves its seller's inventory until it is bought or taken back; a
/// sale's revenue (the price less the selling fee) waits until the seller redeems it.
/// </summary>
public interface IMarketplaceService
{
    /// <summary>Open offers matching a search (Flash <c>GetMarketplaceOffersMessageComposer</c>); -1 is no bound.</summary>
    public Task<MarketplaceOfferPage> SearchOffersAsync(
        int minPrice,
        int maxPrice,
        string query,
        int sortType,
        bool combineUniques,
        CancellationToken ct
    );

    /// <summary>The seller's offers of one category: 1 open, 2 sold, 3 expired.</summary>
    public Task<MarketplaceOwnOffers> GetOwnOffersAsync(
        PlayerId player,
        int category,
        CancellationToken ct
    );

    public Task<MarketplaceItemStatsSnapshot> GetItemStatsAsync(
        int category,
        int furniTypeId,
        string? extraData,
        CancellationToken ct
    );

    public Task<MarketplaceCanMakeOffer> CanMakeOfferAsync(PlayerId player, CancellationToken ct);

    /// <summary>Offers inventory items (by the refs the client lists them under) at one price each.</summary>
    public Task<MarketplaceMakeOfferCode> MakeOfferAsync(
        PlayerId player,
        int price,
        ImmutableArray<int> itemRefs,
        CancellationToken ct
    );

    /// <summary>Buys a batch of offer tokens; false when the credits are short or tokens are off.</summary>
    public Task<bool> BuyTokensAsync(PlayerId player, CancellationToken ct);

    /// <summary>Takes back an open or expired own offer: the item returns to the inventory.</summary>
    public Task<bool> CancelOfferAsync(PlayerId player, int offerId, CancellationToken ct);

    public Task<MarketplaceBuyResult> BuyOfferAsync(
        PlayerId player,
        int offerId,
        CancellationToken ct
    );

    /// <summary>
    /// Takes back every unsold own offer, open or expired (Flash <c>recallAllOffers</c>): the
    /// items return to the inventory. Returns the offer ids taken back.
    /// </summary>
    public Task<ImmutableArray<int>> CancelAllOffersAsync(PlayerId player, CancellationToken ct);

    /// <summary>
    /// Clears the own offers of a clearable category (Flash <c>_-K1l.isClearable</c>: 2 sold,
    /// 3 expired); false for any other.
    /// </summary>
    public Task<bool> ClearOwnHistoryAsync(PlayerId player, int category, CancellationToken ct);

    /// <summary>Pays out the revenue of the seller's sold offers; returns the credits paid.</summary>
    public Task<int> RedeemCreditsAsync(PlayerId player, CancellationToken ct);
}
