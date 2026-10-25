using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Exceptions;
using Turbo.Database.Context;
using Turbo.Players.Configuration;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;

namespace Turbo.Catalog;

public sealed class CatalogService(
    ILogger<ICatalogService> logger,
    ICatalogSnapshotProvider<NormalCatalog> normalCatalogProvider,
    ICatalogSnapshotProvider<BuildersClubCatalog> buildersClubCatalogProvider,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<PlayerConfig> playerConfig,
    IGrainFactory grainFactory,
    IPermissionRegistryProvider permissionRegistryProvider
) : ICatalogService
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IPermissionRegistryProvider _permissionRegistryProvider =
        permissionRegistryProvider;

    // The cuts of a catalog, by which of its gates a player passes: a hotel has a handful of
    // gates and so a handful of cuts, made once each and let go with the catalog they came from
    // (a reload or a limited sale makes a new one).
    private readonly ConditionalWeakTable<CatalogSnapshot, Cuts> _cuts = new();

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

    public async Task<CatalogSnapshot> CutForPlayerAsync(
        CatalogSnapshot catalog,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var cuts = _cuts.GetValue(catalog, x => new Cuts(x));

        if (cuts.Gates.IsEmpty)
            return catalog;

        var permissions = await _grainFactory
            .GetPlayerPermissionGrain(playerId)
            .GetResolvedAsync(ct)
            .ConfigureAwait(false);
        var level = PermissionProjection.SecurityLevelOf(
            _permissionRegistryProvider.Current,
            permissions
        );
        var passed = new StringBuilder(cuts.Gates.Length);

        foreach (var (minRank, node) in cuts.Gates)
            passed.Append(CatalogPageAccess.CanSee(minRank, node, permissions, level) ? '1' : '0');

        return cuts.ByPassed.GetOrAdd(
            passed.ToString(),
            key =>
                CatalogPageAccess.Cut(
                    catalog,
                    page =>
                        !CatalogPageAccess.IsGated(page)
                        || key[cuts.Gates.IndexOf((page.MinRank, page.RequiredNode))] == '1'
                )
        );
    }

    private sealed class Cuts(CatalogSnapshot catalog)
    {
        /// <summary>Every distinct gate a page of the catalog has, in a fixed order.</summary>
        public ImmutableArray<(int MinRank, string? RequiredNode)> Gates { get; } =
        [
            .. catalog
                .PagesById.Values.Where(CatalogPageAccess.IsGated)
                .Select(x => (x.MinRank, x.RequiredNode))
                .Distinct()
                .OrderBy(x => x.MinRank)
                .ThenBy(x => x.RequiredNode, StringComparer.Ordinal),
        ];

        public ConcurrentDictionary<string, CatalogSnapshot> ByPassed { get; } = new();
    }

    public void SetLtdRemaining(int seriesId, int remaining) =>
        _normalCatalogProvider.SetLtdRemaining(seriesId, remaining);

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
                    PriceActivityPointType = offer.ActivityPointType ?? 0,
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
        if (!catalogSnap.TryGetLtdProduct(nextSeries.Id, out var product))
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
