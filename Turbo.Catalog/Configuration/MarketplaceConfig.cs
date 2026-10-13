namespace Turbo.Catalog.Configuration;

/// <summary>
/// The marketplace: what GetMarketplaceConfiguration tells the client, and the limits the server
/// holds offers to. Off by default (a hotel turns it on); the numbers are Habbo's (recorded on
/// hhus, 2026-10-12, 18 samples since).
/// </summary>
public sealed class MarketplaceConfig
{
    public bool Enabled { get; init; } = false;
    public int Commission { get; init; } = 1;
    public int TokenBatchPrice { get; init; } = 0;
    public int TokenBatchSize { get; init; } = 0;
    public int OfferMinPrice { get; init; } = 2;
    public int OfferMaxPrice { get; init; } = 99999;
    public int ExpirationHours { get; init; } = 168;
    public int AveragePricePeriod { get; init; } = 7;
    public int SellingFeePercentage { get; init; } = 2;
    public int RevenueLimit { get; init; } = 100000;
    public int HalfTaxLimit { get; init; } = 400000;

    /// <summary>
    /// Rows a public search answers with at most; the official client capture of 2026-10-13
    /// returned 250 rows of 10972 found for both sorts recorded.
    /// </summary>
    public int OfferListLimit { get; init; } = 250;

    /// <summary>
    /// Items one posting may offer (more is <c>inventory.marketplace.result.9</c>); 250 as the
    /// official <c>inventory.marketplace.make_offer.expiration_info</c> text tells players.
    /// </summary>
    public int MaxItemsPerOffer { get; init; } = 250;
}
