using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Providers;

namespace Turbo.Catalog.Providers;

public sealed class CatalogSnapshotProvider<TTag>(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<ICatalogSnapshotProvider<TTag>> logger,
    IFurnitureDefinitionProvider furnitureProvider,
    CatalogType catalogType
) : ICatalogSnapshotProvider<TTag>
    where TTag : ICatalogTag
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ILogger<ICatalogSnapshotProvider<TTag>> _logger = logger;
    private readonly IFurnitureDefinitionProvider _furnitureProvider = furnitureProvider;
    private CatalogSnapshot _current = CatalogSnapshot.Empty;

    public CatalogType CatalogType => catalogType;
    public CatalogSnapshot Current => _current;

    public async Task<CatalogSnapshot> GetSnapshotAsync(CancellationToken ct)
    {
        if (Current == CatalogSnapshot.Empty)
            await ReloadAsync(ct).ConfigureAwait(false);

        return Current;
    }

    public async Task ReloadAsync(CancellationToken ct)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            // Both catalogs are cut from the one tree of pages. Offers and products have no
            // catalog of their own, so they follow the page that holds them; an offer whose page
            // this catalog does not show must not appear here, because the placement path asks
            // this snapshot whether an offer is one it is allowed to hand out.
            var allPages = await dbCtx
                .CatalogPages.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var tree = CatalogTree.Cut(allPages, catalogType);
            var pages = tree.Pages;
            var offers = (
                await dbCtx.CatalogOffers.AsNoTracking().ToListAsync(ct).ConfigureAwait(false)
            )
                .Where(x => tree.SellingPageIds.Contains(x.CatalogPageEntityId))
                .ToList();
            var offerIdSet = offers.Select(x => x.Id).ToHashSet();
            var products = (
                await dbCtx.CatalogProducts.AsNoTracking().ToListAsync(ct).ConfigureAwait(false)
            )
                .Where(x => offerIdSet.Contains(x.CatalogOfferEntityId))
                .ToList();
            var allSeries = await dbCtx
                .LtdSeries.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);
            // An offer names its currency by its currency_types row; what the wallet and the
            // client go by is that row's activity-point type.
            var activityPointTypes = await dbCtx
                .CurrencyTypes.AsNoTracking()
                .Where(x => x.ActivityPointType != null)
                .ToDictionaryAsync(x => x.Id, x => x.ActivityPointType!.Value, ct)
                .ConfigureAwait(false);

            // Group by product and pick the most relevant series (Active > Newest)
            var series = allSeries
                .GroupBy(s => s.CatalogProductEntityId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                        g.OrderByDescending(s => s.IsActive)
                            .ThenByDescending(s => s.StartsAt ?? s.CreatedAt)
                            .First()
                );

            // The front page's featured items belong to the normal catalog, whose front page
            // draws them.
            var frontPageItems =
                catalogType == CatalogType.Normal
                    ? (
                        await dbCtx
                            .CatalogFeaturedItems.AsNoTracking()
                            .OrderBy(x => x.Position)
                            .ThenBy(x => x.Id)
                            .ToListAsync(ct)
                            .ConfigureAwait(false)
                    )
                        .Select(x => x.ToSnapshot())
                        .ToImmutableArray()
                    : [];

            var pageChildrenIds = tree.ChildIds;

            // A hidden offer stays known by id (Builders Club placement and the purchase check
            // ask), but no page lists it. A page lists its offers in the order the editor set.
            var pageOfferIds = offers
                .Where(o => o.Visible)
                .OrderBy(o => o.SortOrder)
                .ThenBy(o => o.Id)
                .GroupBy(o => o.CatalogPageEntityId)
                .ToImmutableDictionary(g => g.Key, g => g.Select(x => x.Id).ToImmutableArray());

            var offerProductIds = products
                .GroupBy(op => op.CatalogOfferEntityId)
                .ToImmutableDictionary(g => g.Key, g => g.Select(x => x.Id).ToImmutableArray());

            var productsById = products
                .Select(x =>
                    x.ToSnapshot(
                        x.FurnitureDefinitionEntityId is { } definitionId
                            ? _furnitureProvider.TryGetDefinition(definitionId)
                            : null,
                        series.GetValueOrDefault(x.Id)
                    )
                )
                .ToImmutableDictionary(x => x.Id);

            var offersById = offers
                .Select(x =>
                {
                    var ids = offerProductIds.TryGetValue(x.Id, out var productIds)
                        ? productIds
                        : [];
                    var products = ids.Select(x => productsById[x]).ToImmutableArray();

                    int? activityPointType = null;

                    if (x.CurrencyTypeId is { } currencyId)
                    {
                        if (activityPointTypes.TryGetValue(currencyId, out var type))
                            activityPointType = type;
                        else
                            _logger.LogWarning(
                                "Catalog offer {OfferId} is priced in currency type {CurrencyTypeId}, which is not an activity-point currency; its currency price is not charged",
                                x.Id,
                                currencyId
                            );
                    }

                    return x.ToSnapshot(ids, products, activityPointType);
                })
                .ToImmutableDictionary(x => x.Id);

            // A page that is here only to lead to one below it lists none of its own offers.
            var pagesById = pages
                .Select(x =>
                    x.ToSnapshot(
                        tree.SellingPageIds.Contains(x.Id)
                        && pageOfferIds.TryGetValue(x.Id, out var offerIds)
                            ? offerIds
                            : [],
                        pageChildrenIds.TryGetValue(x.Id, out var childIds) ? childIds : []
                    )
                )
                .ToImmutableDictionary(x => x.Id);

            var snapshot = new CatalogSnapshot
            {
                CatalogType = CatalogType,
                // A hotel that has no pages at all is not an error: it simply has no catalog,
                // and the root of nothing is -1.
                RootPageId = tree.RootId,
                PagesById = pagesById,
                OffersById = offersById,
                ProductsById = productsById,
                PageChildrenIds = pageChildrenIds,
                PageOfferIds = pageOfferIds,
                OfferProductIds = offerProductIds,
                // A series is sold by one product; should two claim it, the lower id wins, so
                // the choice does not depend on dictionary order.
                ProductIdByLtdSeriesId = productsById
                    .Values.Where(x => x.LtdSeriesId is not null)
                    .GroupBy(x => x.LtdSeriesId!.Value)
                    .ToImmutableDictionary(g => g.Key, g => g.Min(x => x.Id)),
                FrontPageItems = frontPageItems,
            };

            _logger.LogInformation(
                "Loaded catalog snapshot: Type={CatalogType}, TotalPages={TotalPageCount}, Offers={TotalOfferCount}, Products={TotalProductCount}",
                snapshot.CatalogType,
                snapshot.PagesById.Count,
                snapshot.OffersById.Count,
                snapshot.ProductsById.Count
            );

            Volatile.Write(ref _current, snapshot);
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
