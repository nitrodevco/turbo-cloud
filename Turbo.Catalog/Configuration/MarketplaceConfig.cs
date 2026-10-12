namespace Turbo.Catalog.Configuration;

/// <summary>
/// What GetMarketplaceConfiguration tells the client about the marketplace. Turbo has no
/// marketplace yet, so it is off by default; the numbers are Habbo's (recorded on hhus,
/// 2026-10-12), for when one exists.
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
}
