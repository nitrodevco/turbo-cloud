using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Catalog.Abstractions;
using Turbo.Contracts.Enums.Catalog;
using Turbo.Database.Context;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog;

public sealed class CatalogProvider<TTag>(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    ILogger<ICatalogProvider<TTag>> logger,
    IFurnitureDefinitionProvider furnitureProvider,
    CatalogTypeEnum catalogType
) : ICatalogProvider<TTag>
    where TTag : ICatalogTag
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<ICatalogProvider<TTag>> _logger = logger;
    private readonly IFurnitureDefinitionProvider _furnitureProvider = furnitureProvider;
    private CatalogSnapshot _current = Empty();

    public CatalogSnapshot Current => _current;
    public CatalogTypeEnum CatalogType => catalogType;

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

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

            var pageModels = pages.Select(x => new CatalogPageSnapshot(
                x.Id,
                x.ParentEntityId ?? -1,
                x.Localization,
                x.Name,
                x.Icon,
                x.Layout,
                x.ImageData ?? [],
                x.TextData ?? [],
                x.Visible
            ));

            var offerModels = offers.Select(x => new CatalogOfferSnapshot(
                x.Id,
                x.CatalogPageEntityId,
                x.LocalizationId ?? string.Empty,
                false, // isRentable
                x.CostCredits,
                x.CostCurrency,
                x.CurrencyType,
                0, // CostSilver
                x.CanGift,
                x.CanBundle,
                x.ClubLevel,
                x.Visible
            ));

            var productModels = products.Select(x => new CatalogProductSnapshot(
                x.Id,
                x.CatalogOfferEntityId,
                x.ProductType,
                x.FurnitureDefinitionEntityId ?? -1,
                SpriteId: x.FurnitureDefinitionEntityId != null
                    ? _furnitureProvider
                        .TryGetDefinition(x.FurnitureDefinitionEntityId.Value)
                        ?.SpriteId ?? -1
                    : -1,
                x.ExtraParam,
                x.Quantity,
                x.UniqueSize,
                x.UniqueRemaining
            ));

            var pagesById = pageModels.ToImmutableDictionary(p => p.Id);
            var offersById = offerModels.ToImmutableDictionary(o => o.Id);
            var productsById = productModels.ToImmutableDictionary(p => p.Id);

            var pageChildren = pages
                .GroupBy(p => p.ParentEntityId ?? -1)
                .ToImmutableDictionary(
                    g => g.Key,
                    g => g.OrderBy(x => x.SortOrder).Select(x => x.Id).ToImmutableArray()
                );

            var pageOffers = offers
                .GroupBy(o => o.CatalogPageEntityId)
                .ToImmutableDictionary(g => g.Key, g => g.Select(x => x.Id).ToImmutableArray());

            var offerProductsMap = products
                .GroupBy(op => op.CatalogOfferEntityId)
                .ToImmutableDictionary(g => g.Key, g => g.Select(x => x.Id).ToImmutableArray());

            var snapshot = new CatalogSnapshot(
                CatalogType: CatalogType,
                PagesById: pagesById,
                OffersById: offersById,
                ProductsById: productsById,
                PageChildren: pageChildren,
                PageOffers: pageOffers,
                OfferProducts: offerProductsMap
            );

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

    private static CatalogSnapshot Empty() =>
        new(
            CatalogTypeEnum.Normal,
            ImmutableDictionary<int, CatalogPageSnapshot>.Empty,
            ImmutableDictionary<int, CatalogOfferSnapshot>.Empty,
            ImmutableDictionary<int, CatalogProductSnapshot>.Empty,
            ImmutableDictionary<int, ImmutableArray<int>>.Empty,
            ImmutableDictionary<int, ImmutableArray<int>>.Empty,
            ImmutableDictionary<int, ImmutableArray<int>>.Empty
        );
}
