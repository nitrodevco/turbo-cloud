using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Catalog.Exceptions;
using Turbo.Database.Context;
using Turbo.Players.Configuration;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Catalog;

public sealed class CatalogService(
    ILogger<ICatalogService> logger,
    ICatalogSnapshotProvider<NormalCatalog> normalCatalogProvider,
    ICatalogSnapshotProvider<BuildersClubCatalog> buildersClubCatalogProvider,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<PlayerConfig> playerConfig
) : ICatalogService
{
    private readonly ILogger<ICatalogService> _logger = logger;
    private readonly ICatalogSnapshotProvider<NormalCatalog> _normalCatalogProvider =
        normalCatalogProvider;
    private readonly ICatalogSnapshotProvider<BuildersClubCatalog> _buildersClubCatalogProvider =
        buildersClubCatalogProvider;
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;

    // The client adds a membership back up as periods * 31 + days, so how long a period is has
    // to be the one the subscriptions themselves are measured in.
    private readonly int _daysPerPeriod = Math.Max(
        1,
        playerConfig.Value.Subscriptions.DaysPerPeriod
    );

    public CatalogSnapshot GetCatalogSnapshot(CatalogType catalogType)
    {
        return catalogType switch
        {
            CatalogType.Normal => _normalCatalogProvider.Current,
            CatalogType.BuildersClub => _buildersClubCatalogProvider.Current,
            _ => throw new CatalogTypeNotSupportedException(catalogType),
        };
    }

    /// <summary>
    /// Walks the normal catalog for offers whose product grants Habbo Club days. There are a
    /// handful of them and the client asks twice a session (the catalog opening and the club
    /// centre), so this is worked out each time rather than cached beside a snapshot that is
    /// replaced wholesale on reload.
    /// </summary>
    public ImmutableArray<ClubOfferSnapshot> GetClubOffers()
    {
        var snapshot = GetCatalogSnapshot(CatalogType.Normal);
        var offers = ImmutableArray.CreateBuilder<ClubOfferSnapshot>();

        foreach (var offer in snapshot.OffersById.Values)
        {
            if (!offer.Visible)
                continue;

            var days = offer
                .Products.Where(x => x.SubscriptionType == SubscriptionType.HabboClub)
                .Sum(x => x.SubscriptionDays);

            if (days <= 0)
                continue;

            offers.Add(
                new ClubOfferSnapshot
                {
                    OfferId = offer.Id,
                    ProductCode = offer.LocalizationId,
                    PriceCredits = offer.CostCredits,
                    PriceActivityPoints = offer.CostCurrency,
                    PriceActivityPointType = offer.CurrencyTypeId ?? -1,
                    IsVip = true,
                    Months = days / _daysPerPeriod,
                    ExtraDays = days % _daysPerPeriod,
                    IsGiftable = offer.CanGift,
                    Days = days,
                    DaysLeftAfterPurchase = 0,
                    Year = 0,
                    Month = 0,
                    Day = 0,
                }
            );
        }

        offers.Sort((left, right) => left.Days.CompareTo(right.Days));

        return offers.ToImmutable();
    }

    public ImmutableArray<CatalogOfferSnapshot> GetClubGiftOffers() =>
        [
            .. GetCatalogSnapshot(CatalogType.Normal)
                .OffersById.Values.Where(x => x.Visible && x.ClubGiftDaysRequired is not null)
                .OrderBy(x => x.ClubGiftDaysRequired),
        ];

    public ClubExtendOfferSnapshot? GetClubExtendOffer()
    {
        // Only offers of at least one whole period can be renewals: the client multiplies the
        // per-period price by the offer's months to draw the normal price, and would show
        // nothing for an offer of fewer days than a period.
        var offers = GetClubOffers().Where(x => x.Months > 0).ToList();

        if (offers.Count == 0)
        {
            _logger.LogDebug("No Habbo Club offer of a whole period is on sale to renew with");

            return null;
        }

        var renewal = offers.MaxBy(x => x.Days)!;

        // What a period normally costs is not invented: it is what the hotel charges for its
        // shortest membership, so a longer one priced below that many periods shows a saving and
        // a hotel that gives no discount shows none.
        var shortest = offers.MinBy(x => x.Days)!;

        return new ClubExtendOfferSnapshot(renewal)
        {
            OriginalPriceCreditsPerPeriod = shortest.PriceCredits / shortest.Months,
            OriginalPriceActivityPointsPerPeriod = shortest.PriceActivityPoints / shortest.Months,
            OriginalActivityPointType = shortest.PriceActivityPointType,
            SubscriptionDaysLeft = 0,
        };
    }

    public async Task<UpcomingLtdSnapshot?> GetUpcomingLtdAsync(CancellationToken ct)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        // Find the nearest upcoming active LTD drop
        var now = DateTime.UtcNow;
        var nextSeries = await dbCtx
            .LtdSeries.AsNoTracking()
            .Where(s => s.IsActive && s.StartsAt > now)
            .OrderBy(s => s.StartsAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (nextSeries == null)
            return null;

        var catalogSnap = GetCatalogSnapshot(CatalogType.Normal);
        var product = catalogSnap.ProductsById.Values.FirstOrDefault(p =>
            p.LtdSeriesId == nextSeries.Id
        );

        if (product == null)
            return null;

        // Resolve PageId from Offer
        if (!catalogSnap.OffersById.TryGetValue(product.OfferId, out var offer))
            return null;

        return new UpcomingLtdSnapshot
        {
            SecondsUntil = (int)(nextSeries.StartsAt!.Value - now).TotalSeconds,
            PageId = offer.PageId,
            OfferId = offer.Id,
            ClassName = product.ClassName,
        };
    }
}
