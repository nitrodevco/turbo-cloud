using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
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
            var pages = await dbCtx
                .CatalogPages.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var offers = await dbCtx
                .CatalogOffers.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var products = await dbCtx
                .CatalogProducts.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var pageChildrenIds = pages
                .GroupBy(p => p.ParentEntityId ?? -1)
                .ToImmutableDictionary(
                    g => g.Key,
                    g =>
                        g.OrderBy(x => x.SortOrder)
                            .ThenBy(x => x.Localization)
                            .Select(x => x.Id)
                            .ToImmutableArray()
                );

            var pageOfferIds = offers
                .GroupBy(o => o.CatalogPageEntityId)
                .ToImmutableDictionary(g => g.Key, g => g.Select(x => x.Id).ToImmutableArray());

            var offerProductIds = products
                .GroupBy(op => op.CatalogOfferEntityId)
                .ToImmutableDictionary(g => g.Key, g => g.Select(x => x.Id).ToImmutableArray());

            var productsById = products
                .Select(x => new CatalogProductSnapshot
                {
                    Id = x.Id,
                    OfferId = x.CatalogOfferEntityId,
                    ProductType = x.ProductType,
                    FurniDefinitionId = x.FurnitureDefinitionEntityId ?? -1,
                    SpriteId =
                        x.FurnitureDefinitionEntityId != null
                            ? _furnitureProvider
                                .TryGetDefinition(x.FurnitureDefinitionEntityId.Value)
                                ?.SpriteId ?? -1
                            : -1,
                    ExtraParam = x.ExtraParam,
                    Quantity = x.Quantity,
                    UniqueSize = x.UniqueSize,
                    UniqueRemaining = x.UniqueRemaining,
                })
                .ToImmutableDictionary(x => x.Id);

            var offersById = offers
                .Select(x =>
                {
                    var ids = offerProductIds.TryGetValue(x.Id, out var productIds)
                        ? productIds
                        : [];
                    var products = ids.Select(x => productsById[x]).ToImmutableArray();

                    return new CatalogOfferSnapshot()
                    {
                        Id = x.Id,
                        PageId = x.CatalogPageEntityId,
                        LocalizationId = x.LocalizationId ?? string.Empty,
                        Rentable = false,
                        CostCredits = x.CostCredits,
                        CostCurrency = x.CostCurrency,
                        CurrencyType = x.CurrencyType,
                        CostSilver = 0,
                        CanGift = x.CanGift,
                        CanBundle = x.CanBundle,
                        ClubLevel = x.ClubLevel,
                        Visible = x.Visible,
                        ProductIds = ids,
                        Products = products,
                    };
                })
                .ToImmutableDictionary(x => x.Id);

            var pagesById = pages
                .Select(x => new CatalogPageSnapshot
                {
                    Id = x.Id,
                    ParentId = x.ParentEntityId ?? -1,
                    Localization = x.Localization,
                    Name = x.Name,
                    Icon = x.Icon,
                    Layout = x.Layout,
                    ImageData = x.ImageData ?? [],
                    TextData = x.TextData ?? [],
                    Visible = x.Visible,
                    OfferIds = pageOfferIds.TryGetValue(x.Id, out var offerIds) ? offerIds : [],
                    ChildIds = pageChildrenIds.TryGetValue(x.Id, out var childIds) ? childIds : [],
                })
                .ToImmutableDictionary(x => x.Id);

            var snapshot = new CatalogSnapshot
            {
                CatalogType = CatalogType,
                RootPageId = pages.First(x => x.ParentEntityId == null)?.Id ?? -1,
                PagesById = pagesById,
                OffersById = offersById,
                ProductsById = productsById,
                PageChildrenIds = pageChildrenIds,
                PageOfferIds = pageOfferIds,
                OfferProductIds = offerProductIds,
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
