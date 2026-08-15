using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog.Admin;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;

namespace Turbo.Catalog.Grains;

public sealed class CatalogAdminGrain(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ICatalogSnapshotProvider<NormalCatalog> catalogSnapshotProvider,
    ILogger<CatalogAdminGrain> logger
) : Grain, ICatalogAdminGrain
{
    public async Task<int> CreatePageAsync(CreateCatalogPageInput input, CancellationToken ct)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var page = new CatalogPageEntity
        {
            ParentEntityId = input.ParentId,
            Localization = input.Localization,
            Name = input.Name,
            Icon = input.Icon,
            Layout = input.Layout,
            ImageData = input.ImageData,
            TextData = input.TextData,
            SortOrder = 0,
            Visible = input.Visible,
        };

        dbCtx.Add(page);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        await catalogSnapshotProvider.ReloadAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Catalog page {PageId} created.", page.Id);

        return page.Id;
    }

    public async Task UpdatePageAsync(
        int pageId,
        UpdateCatalogPageInput input,
        CancellationToken ct
    )
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var affected = await dbCtx
            .CatalogPages.Where(x => x.Id == pageId)
            .ExecuteUpdateAsync(
                up =>
                    up.SetProperty(x => x.Localization, input.Localization)
                        .SetProperty(x => x.Name, input.Name)
                        .SetProperty(x => x.Icon, input.Icon)
                        .SetProperty(x => x.Layout, input.Layout)
                        .SetProperty(x => x.ImageData, input.ImageData)
                        .SetProperty(x => x.TextData, input.TextData)
                        .SetProperty(x => x.Visible, input.Visible),
                ct
            )
            .ConfigureAwait(false);

        if (affected == 0)
            throw new InvalidOperationException($"Catalog page {pageId} does not exist.");

        await catalogSnapshotProvider.ReloadAsync(ct).ConfigureAwait(false);
    }

    public async Task DeletePageAsync(int pageId, CancellationToken ct)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var hasChildren = await dbCtx
            .CatalogPages.AnyAsync(x => x.ParentEntityId == pageId, ct)
            .ConfigureAwait(false);

        if (hasChildren)
            throw new InvalidOperationException(
                "This page has subpages. Move or delete them first."
            );

        await dbCtx
            .CatalogProducts.Where(x => x.Offer.CatalogPageEntityId == pageId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        await dbCtx
            .CatalogOffers.Where(x => x.CatalogPageEntityId == pageId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        var affected = await dbCtx
            .CatalogPages.Where(x => x.Id == pageId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        if (affected == 0)
            throw new InvalidOperationException($"Catalog page {pageId} does not exist.");

        await catalogSnapshotProvider.ReloadAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Catalog page {PageId} deleted.", pageId);
    }

    public async Task ReorderPagesAsync(List<CatalogPageOrderEntry> entries, CancellationToken ct)
    {
        if (entries.Count == 0)
            return;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var transaction = await dbCtx
            .Database.BeginTransactionAsync(ct)
            .ConfigureAwait(false);

        foreach (var entry in entries)
        {
            await dbCtx
                .CatalogPages.Where(x => x.Id == entry.PageId)
                .ExecuteUpdateAsync(
                    up =>
                        up.SetProperty(x => x.ParentEntityId, entry.ParentId)
                            .SetProperty(x => x.SortOrder, entry.SortOrder),
                    ct
                )
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);

        await catalogSnapshotProvider.ReloadAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> CreateOfferAsync(CreateCatalogOfferInput input, CancellationToken ct)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var offer = new CatalogOfferEntity
        {
            CatalogPageEntityId = input.PageId,
            LocalizationId = input.LocalizationId,
            CostCredits = input.CostCredits,
            CostCurrency = input.CostCurrency,
            CurrencyTypeId = input.CurrencyTypeId,
            CanGift = input.CanGift,
            CanBundle = input.CanBundle,
            ClubLevel = input.ClubLevel,
            Visible = input.Visible,
            Page = null!,
        };

        dbCtx.Add(offer);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        await catalogSnapshotProvider.ReloadAsync(ct).ConfigureAwait(false);

        return offer.Id;
    }

    public async Task UpdateOfferAsync(
        int offerId,
        UpdateCatalogOfferInput input,
        CancellationToken ct
    )
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var affected = await dbCtx
            .CatalogOffers.Where(x => x.Id == offerId)
            .ExecuteUpdateAsync(
                up =>
                    up.SetProperty(x => x.CatalogPageEntityId, input.PageId)
                        .SetProperty(x => x.LocalizationId, input.LocalizationId)
                        .SetProperty(x => x.CostCredits, input.CostCredits)
                        .SetProperty(x => x.CostCurrency, input.CostCurrency)
                        .SetProperty(x => x.CurrencyTypeId, input.CurrencyTypeId)
                        .SetProperty(x => x.CanGift, input.CanGift)
                        .SetProperty(x => x.CanBundle, input.CanBundle)
                        .SetProperty(x => x.ClubLevel, input.ClubLevel)
                        .SetProperty(x => x.Visible, input.Visible),
                ct
            )
            .ConfigureAwait(false);

        if (affected == 0)
            throw new InvalidOperationException($"Catalog offer {offerId} does not exist.");

        await catalogSnapshotProvider.ReloadAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteOfferAsync(int offerId, CancellationToken ct)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        await dbCtx
            .CatalogProducts.Where(x => x.CatalogOfferEntityId == offerId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        var affected = await dbCtx
            .CatalogOffers.Where(x => x.Id == offerId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        if (affected == 0)
            throw new InvalidOperationException($"Catalog offer {offerId} does not exist.");

        await catalogSnapshotProvider.ReloadAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> CreateProductAsync(CreateCatalogProductInput input, CancellationToken ct)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var product = new CatalogProductEntity
        {
            CatalogOfferEntityId = input.OfferId,
            ProductType = input.ProductType,
            FurnitureDefinitionEntityId = input.FurnitureDefinitionId,
            ExtraParam = input.ExtraParam,
            Quantity = input.Quantity,
            Offer = null!,
        };

        dbCtx.Add(product);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        await catalogSnapshotProvider.ReloadAsync(ct).ConfigureAwait(false);

        return product.Id;
    }

    public async Task UpdateProductAsync(
        int productId,
        UpdateCatalogProductInput input,
        CancellationToken ct
    )
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var affected = await dbCtx
            .CatalogProducts.Where(x => x.Id == productId)
            .ExecuteUpdateAsync(
                up =>
                    up.SetProperty(x => x.ProductType, input.ProductType)
                        .SetProperty(
                            x => x.FurnitureDefinitionEntityId,
                            input.FurnitureDefinitionId
                        )
                        .SetProperty(x => x.ExtraParam, input.ExtraParam)
                        .SetProperty(x => x.Quantity, input.Quantity),
                ct
            )
            .ConfigureAwait(false);

        if (affected == 0)
            throw new InvalidOperationException($"Catalog product {productId} does not exist.");

        await catalogSnapshotProvider.ReloadAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteProductAsync(int productId, CancellationToken ct)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var affected = await dbCtx
            .CatalogProducts.Where(x => x.Id == productId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        if (affected == 0)
            throw new InvalidOperationException($"Catalog product {productId} does not exist.");

        await catalogSnapshotProvider.ReloadAsync(ct).ConfigureAwait(false);
    }
}
