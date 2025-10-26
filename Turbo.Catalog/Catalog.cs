using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Catalog.Abstractions;
using Turbo.Contracts.Enums.Catalog;
using Turbo.Database.Context;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog;

public sealed class Catalog(
    ICatalogFactory catalogFactory,
    IDbContextFactory<TurboDbContext> dbContextFactory,
    CatalogTypeEnum catalogType
) : ICatalog
{
    private readonly ICatalogFactory _catalogFactory = catalogFactory;
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    public CatalogTypeEnum CatalogType { get; } = catalogType;
    public ICatalogPage? RootPage { get; private set; }
    public IDictionary<int, ICatalogPage> Pages { get; } = new Dictionary<int, ICatalogPage>();
    public IDictionary<int, ICatalogOffer> Offers { get; } = new Dictionary<int, ICatalogOffer>();
    public IDictionary<int, ICatalogProduct> Products { get; } =
        new Dictionary<int, ICatalogProduct>();

    private void BuildCatalog()
    {
        var root = _catalogFactory.CreateCatalogRoot();

        Pages.Add(root.Snapshot.Id, root);

        foreach (var offer in Offers.Values)
        {
            if (offer is null || !Pages.ContainsKey(offer.Snapshot.PageId))
                continue;

            offer.SetPage(Pages[offer.Snapshot.PageId]);
        }

        foreach (var product in Products.Values)
        {
            if (product is null || !Offers.ContainsKey(product.Snapshot.OfferId))
                continue;

            product.SetOffer(Offers[product.Snapshot.OfferId]);
        }

        foreach (var page in Pages.Values)
        {
            if (page is null || !Pages.ContainsKey(page.Snapshot.ParentId))
                continue;

            page.SetParent(Pages[page.Snapshot.ParentId]);
        }

        RootPage = root;
    }

    public async ValueTask LoadCatalogAsync(CancellationToken ct)
    {
        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var pageEntities = await dbCtx
                .CatalogPages.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var offerEntities = await dbCtx
                .CatalogOffers.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var productEntities = await dbCtx
                .CatalogProducts.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            pageEntities.ForEach(x =>
            {
                if (x.Visible is false)
                    return;

                var pageSnapshot = new CatalogPageSnapshot(
                    x.Id,
                    x.ParentEntityId ?? -1,
                    x.Localization,
                    x.Name,
                    x.Icon,
                    x.Layout,
                    x.ImageData,
                    x.TextData,
                    x.Visible
                );

                var page = _catalogFactory.CreateCatalogPage(pageSnapshot);

                Pages.Add(page.Snapshot.Id, page);
            });

            offerEntities.ForEach(x =>
            {
                var offerSnapshot = new CatalogOfferSnapshot(
                    x.Id,
                    x.CatalogPageEntityId,
                    x.LocalizationId ?? string.Empty,
                    x.CostCredits,
                    x.CostCurrency,
                    x.CurrencyType,
                    x.CanGift,
                    x.CanBundle,
                    x.ClubLevel,
                    x.Visible
                );

                var offer = _catalogFactory.CreateCatalogOffer(offerSnapshot);

                Offers.Add(offer.Snapshot.Id, offer);
            });

            productEntities.ForEach(x =>
            {
                var productSnapshot = new CatalogProductSnapshot(
                    x.Id,
                    x.CatalogOfferEntityId,
                    x.ProductType,
                    x.FurnitureDefinitionEntityId,
                    x.ExtraParam,
                    x.Quantity,
                    x.UniqueSize,
                    x.UniqueRemaining
                );

                var product = _catalogFactory.CreateCatalogProduct(productSnapshot);

                Products.Add(product.Snapshot.Id, product);
            });

            BuildCatalog();
        }
        catch (Exception ex)
        {
            throw;
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
