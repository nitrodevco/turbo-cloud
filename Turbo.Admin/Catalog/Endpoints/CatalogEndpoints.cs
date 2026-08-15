using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Admin.Catalog.Contracts;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog.Admin;
using Turbo.Primitives.Orleans;

namespace Turbo.Admin.Catalog.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapAdminCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/catalog").RequireAuthorization();

        group.MapGet(
            "/pages",
            async (IDbContextFactory<TurboDbContext> dbCtxFactory, CancellationToken ct) =>
            {
                await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

                var rows = await dbCtx
                    .CatalogPages.AsNoTracking()
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Localization)
                    .Select(x => new
                    {
                        x.Id,
                        x.ParentEntityId,
                        x.Localization,
                        x.Name,
                        x.Icon,
                        x.Layout,
                        x.SortOrder,
                        x.Visible,
                    })
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var offerCounts = await dbCtx
                    .CatalogOffers.AsNoTracking()
                    .GroupBy(x => x.CatalogPageEntityId)
                    .Select(g => new { PageId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.PageId, x => x.Count, ct)
                    .ConfigureAwait(false);

                var byParent = rows.ToLookup(x => x.ParentEntityId);

                List<CatalogPageTreeItem> BuildChildren(int? parentId) =>
                    byParent[parentId]
                        .Select(row => new CatalogPageTreeItem(
                            row.Id,
                            row.ParentEntityId,
                            row.Localization,
                            row.Name,
                            row.Icon,
                            row.Layout,
                            row.SortOrder,
                            row.Visible,
                            offerCounts.GetValueOrDefault(row.Id),
                            BuildChildren(row.Id)
                        ))
                        .ToList();

                return Results.Ok(BuildChildren(null));
            }
        );

        group.MapGet(
            "/pages/{id:int}",
            async (int id, IDbContextFactory<TurboDbContext> dbCtxFactory, CancellationToken ct) =>
            {
                await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

                var page = await dbCtx
                    .CatalogPages.AsNoTracking()
                    .Where(x => x.Id == id)
                    .SingleOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                if (page is null)
                    return Results.NotFound();

                var offers = await dbCtx
                    .CatalogOffers.AsNoTracking()
                    .Where(x => x.CatalogPageEntityId == id)
                    .Select(x => new CatalogOfferSummary(
                        x.Id,
                        x.LocalizationId,
                        x.CostCredits,
                        x.CostCurrency,
                        x.CurrencyTypeId,
                        x.ClubLevel,
                        x.Visible,
                        x.Products!.Count
                    ))
                    .ToArrayAsync(ct)
                    .ConfigureAwait(false);

                return Results.Ok(
                    new CatalogPageDetailResponse(
                        page.Id,
                        page.ParentEntityId,
                        page.Localization,
                        page.Name,
                        page.Icon,
                        page.Layout,
                        page.ImageData ?? [],
                        page.TextData ?? [],
                        page.Visible,
                        offers
                    )
                );
            }
        );

        group.MapPost(
            "/pages",
            async (UpsertPageRequest request, IGrainFactory grainFactory, CancellationToken ct) =>
            {
                var id = await grainFactory
                    .GetCatalogAdminGrain()
                    .CreatePageAsync(
                        new CreateCatalogPageInput
                        {
                            ParentId = request.ParentId,
                            Localization = request.Localization,
                            Name = request.Name,
                            Icon = request.Icon,
                            Layout = request.Layout,
                            ImageData = request.ImageData,
                            TextData = request.TextData,
                            Visible = request.Visible,
                        },
                        ct
                    )
                    .ConfigureAwait(false);

                return Results.Ok(new CreatedResponse(id));
            }
        );

        group.MapPut(
            "/pages/{id:int}",
            async (
                int id,
                UpsertPageRequest request,
                IGrainFactory grainFactory,
                CancellationToken ct
            ) =>
            {
                try
                {
                    await grainFactory
                        .GetCatalogAdminGrain()
                        .UpdatePageAsync(
                            id,
                            new UpdateCatalogPageInput
                            {
                                Localization = request.Localization,
                                Name = request.Name,
                                Icon = request.Icon,
                                Layout = request.Layout,
                                ImageData = request.ImageData,
                                TextData = request.TextData,
                                Visible = request.Visible,
                            },
                            ct
                        )
                        .ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }

                return Results.NoContent();
            }
        );

        group.MapDelete(
            "/pages/{id:int}",
            async (int id, IGrainFactory grainFactory, CancellationToken ct) =>
            {
                try
                {
                    await grainFactory
                        .GetCatalogAdminGrain()
                        .DeletePageAsync(id, ct)
                        .ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }

                return Results.NoContent();
            }
        );

        group.MapPost(
            "/pages/reorder",
            async (ReorderPagesRequest request, IGrainFactory grainFactory, CancellationToken ct) =>
            {
                var entries = request
                    .Entries.Select(x => new CatalogPageOrderEntry
                    {
                        PageId = x.PageId,
                        ParentId = x.ParentId,
                        SortOrder = x.SortOrder,
                    })
                    .ToList();

                await grainFactory
                    .GetCatalogAdminGrain()
                    .ReorderPagesAsync(entries, ct)
                    .ConfigureAwait(false);

                return Results.NoContent();
            }
        );

        group.MapGet(
            "/offers/{id:int}",
            async (int id, IDbContextFactory<TurboDbContext> dbCtxFactory, CancellationToken ct) =>
            {
                await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

                var offer = await dbCtx
                    .CatalogOffers.AsNoTracking()
                    .Where(x => x.Id == id)
                    .SingleOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                if (offer is null)
                    return Results.NotFound();

                var products = await dbCtx
                    .CatalogProducts.AsNoTracking()
                    .Where(x => x.CatalogOfferEntityId == id)
                    .Select(x => new CatalogProductItem(
                        x.Id,
                        x.ProductType,
                        x.FurnitureDefinitionEntityId,
                        x.FurnitureDefinition != null ? x.FurnitureDefinition.Name : null,
                        x.FurnitureDefinition != null ? x.FurnitureDefinition.SpriteId : null,
                        x.ExtraParam,
                        x.Quantity
                    ))
                    .ToArrayAsync(ct)
                    .ConfigureAwait(false);

                return Results.Ok(
                    new CatalogOfferDetailResponse(
                        offer.Id,
                        offer.CatalogPageEntityId,
                        offer.LocalizationId,
                        offer.CostCredits,
                        offer.CostCurrency,
                        offer.CurrencyTypeId,
                        offer.CanGift,
                        offer.CanBundle,
                        offer.ClubLevel,
                        offer.Visible,
                        products
                    )
                );
            }
        );

        group.MapPost(
            "/offers",
            async (UpsertOfferRequest request, IGrainFactory grainFactory, CancellationToken ct) =>
            {
                var id = await grainFactory
                    .GetCatalogAdminGrain()
                    .CreateOfferAsync(
                        new CreateCatalogOfferInput
                        {
                            PageId = request.PageId,
                            LocalizationId = request.LocalizationId,
                            CostCredits = request.CostCredits,
                            CostCurrency = request.CostCurrency,
                            CurrencyTypeId = request.CurrencyTypeId,
                            CanGift = request.CanGift,
                            CanBundle = request.CanBundle,
                            ClubLevel = request.ClubLevel,
                            Visible = request.Visible,
                        },
                        ct
                    )
                    .ConfigureAwait(false);

                return Results.Ok(new CreatedResponse(id));
            }
        );

        group.MapPut(
            "/offers/{id:int}",
            async (
                int id,
                UpsertOfferRequest request,
                IGrainFactory grainFactory,
                CancellationToken ct
            ) =>
            {
                try
                {
                    await grainFactory
                        .GetCatalogAdminGrain()
                        .UpdateOfferAsync(
                            id,
                            new UpdateCatalogOfferInput
                            {
                                PageId = request.PageId,
                                LocalizationId = request.LocalizationId,
                                CostCredits = request.CostCredits,
                                CostCurrency = request.CostCurrency,
                                CurrencyTypeId = request.CurrencyTypeId,
                                CanGift = request.CanGift,
                                CanBundle = request.CanBundle,
                                ClubLevel = request.ClubLevel,
                                Visible = request.Visible,
                            },
                            ct
                        )
                        .ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }

                return Results.NoContent();
            }
        );

        group.MapDelete(
            "/offers/{id:int}",
            async (int id, IGrainFactory grainFactory, CancellationToken ct) =>
            {
                try
                {
                    await grainFactory
                        .GetCatalogAdminGrain()
                        .DeleteOfferAsync(id, ct)
                        .ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }

                return Results.NoContent();
            }
        );

        group.MapPost(
            "/products",
            async (
                UpsertProductRequest request,
                IGrainFactory grainFactory,
                CancellationToken ct
            ) =>
            {
                var id = await grainFactory
                    .GetCatalogAdminGrain()
                    .CreateProductAsync(
                        new CreateCatalogProductInput
                        {
                            OfferId = request.OfferId,
                            ProductType = request.ProductType,
                            FurnitureDefinitionId = request.FurnitureDefinitionId,
                            ExtraParam = request.ExtraParam,
                            Quantity = request.Quantity,
                        },
                        ct
                    )
                    .ConfigureAwait(false);

                return Results.Ok(new CreatedResponse(id));
            }
        );

        group.MapPut(
            "/products/{id:int}",
            async (
                int id,
                UpsertProductRequest request,
                IGrainFactory grainFactory,
                CancellationToken ct
            ) =>
            {
                try
                {
                    await grainFactory
                        .GetCatalogAdminGrain()
                        .UpdateProductAsync(
                            id,
                            new UpdateCatalogProductInput
                            {
                                ProductType = request.ProductType,
                                FurnitureDefinitionId = request.FurnitureDefinitionId,
                                ExtraParam = request.ExtraParam,
                                Quantity = request.Quantity,
                            },
                            ct
                        )
                        .ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }

                return Results.NoContent();
            }
        );

        group.MapDelete(
            "/products/{id:int}",
            async (int id, IGrainFactory grainFactory, CancellationToken ct) =>
            {
                try
                {
                    await grainFactory
                        .GetCatalogAdminGrain()
                        .DeleteProductAsync(id, ct)
                        .ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }

                return Results.NoContent();
            }
        );

        group.MapGet(
            "/furniture",
            async (
                string? search,
                IDbContextFactory<TurboDbContext> dbCtxFactory,
                CancellationToken ct
            ) =>
            {
                await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

                var query = dbCtx.FurnitureDefinitions.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(x => x.Name.Contains(search));

                var items = await query
                    .OrderBy(x => x.Name)
                    .Take(30)
                    .Select(x => new FurnitureDefinitionListItem(
                        x.Id,
                        x.Name,
                        x.SpriteId,
                        x.ProductType
                    ))
                    .ToArrayAsync(ct)
                    .ConfigureAwait(false);

                return Results.Ok(items);
            }
        );

        group.MapGet(
            "/currency-types",
            async (IDbContextFactory<TurboDbContext> dbCtxFactory, CancellationToken ct) =>
            {
                await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

                var items = await dbCtx
                    .CurrencyTypes.AsNoTracking()
                    .Where(x => x.Enabled)
                    .OrderBy(x => x.Name)
                    .Select(x => new CurrencyTypeListItem(
                        x.Id,
                        x.Name ?? string.Empty,
                        x.CurrencyType
                    ))
                    .ToArrayAsync(ct)
                    .ConfigureAwait(false);

                return Results.Ok(items);
            }
        );

        return app;
    }
}
