using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Admin.Catalog;

/// <summary>
/// The catalog as the editor reads it: the rows as they are saved, published or not, so an edit
/// shows at once. Read-only; changes go through <see cref="ICatalogEditService"/>.
/// </summary>
public sealed class AdminCatalogQueries(
    IDbContextFactory<TurboDbContext> database,
    IFurnitureDefinitionProvider definitions,
    ICatalogEditService editor,
    IOptions<AdminConfig> config
)
{
    /// <summary>The layout whose page is the club window: it lists every shown membership.</summary>
    public const string CLUB_BUY = "club_buy";

    /// <summary>The layout whose page lists the club gifts members claim.</summary>
    public const string CLUB_GIFTS = "club_gifts";

    public async Task<CatalogTreeResponse> GetTreeAsync(
        CatalogType type,
        bool canManage,
        CancellationToken ct
    )
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var pages = await db
            .CatalogPages.AsNoTracking()
            .Where(x => x.CatalogType == type)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Localization)
            .ThenBy(x => x.Id)
            .Select(x => new CatalogPageNode(
                x.Id,
                x.ParentEntityId,
                x.Localization,
                x.Name,
                x.Icon,
                x.Visible,
                x.SortOrder,
                x.Offers!.Count
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var currencies = await db
            .CurrencyTypes.AsNoTracking()
            .Where(x => x.ActivityPointType != null && x.Enabled)
            .OrderBy(x => x.Id)
            .Select(x => new CatalogCurrencyItem(
                x.Id,
                x.Name ?? string.Empty,
                x.ActivityPointType!.Value
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var used = await db
            .CatalogPages.AsNoTracking()
            .Select(x => x.Layout)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var club =
            type == CatalogType.Normal ? await ClubAsync(db, ct).ConfigureAwait(false) : null;

        return new CatalogTreeResponse(
            type.ToString(),
            pages.FirstOrDefault(x => x.ParentId is null)?.Id ?? 0,
            [.. pages],
            canManage,
            editor.UnpublishedChanges,
            [
                .. currencies.Select(x =>
                    x.Name.Length > 0 ? x : x with { Name = $"type {x.ActivityPointType}" }
                ),
            ],
            [.. config.Value.CatalogLayouts.Concat(used).Distinct().Order(StringComparer.Ordinal)],
            config.Value.CatalogImageUrl,
            club
        );
    }

    /// <summary>
    /// The club shop as players reach it: the shown memberships and gifts (the club window lists
    /// them wherever they are), and the shown pages with the layouts that open it.
    /// </summary>
    private static async Task<CatalogClubSummary> ClubAsync(TurboDbContext db, CancellationToken ct)
    {
        var normal = db
            .CatalogOffers.AsNoTracking()
            .Where(x => x.Visible && x.Page.CatalogType == CatalogType.Normal);
        var memberships = await normal
            .CountAsync(
                x =>
                    x.Products!.Any(p =>
                        p.SubscriptionType == SubscriptionType.HabboClub && p.SubscriptionDays > 0
                    ),
                ct
            )
            .ConfigureAwait(false);
        var gifts = await normal
            .CountAsync(x => x.ClubGiftDaysRequired != null, ct)
            .ConfigureAwait(false);
        var pages = await db
            .CatalogPages.AsNoTracking()
            .Where(x =>
                x.Visible
                && x.CatalogType == CatalogType.Normal
                && (x.Layout == CLUB_BUY || x.Layout == CLUB_GIFTS)
            )
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Layout })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new CatalogClubSummary(
            memberships,
            gifts,
            pages.FirstOrDefault(x => x.Layout == CLUB_BUY)?.Id,
            pages.FirstOrDefault(x => x.Layout == CLUB_GIFTS)?.Id
        );
    }

    /// <summary>One page and its offers; null when there is no such page.</summary>
    public async Task<CatalogPageDetail?> GetPageAsync(int pageId, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var page = await db
            .CatalogPages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == pageId, ct)
            .ConfigureAwait(false);

        if (page is null)
            return null;

        var offers = await db
            .CatalogOffers.AsNoTracking()
            .Where(x => x.CatalogPageEntityId == pageId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var offerIds = offers.Select(x => x.Id).ToList();
        var products = await db
            .CatalogProducts.AsNoTracking()
            .Where(x => offerIds.Contains(x.CatalogOfferEntityId))
            .OrderBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var productIds = products.Select(x => x.Id).ToList();
        // The series a product sells, as the catalog picks it: the active one, else the newest.
        var series = (
            await db
                .LtdSeries.AsNoTracking()
                .Where(x => productIds.Contains(x.CatalogProductEntityId))
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            .GroupBy(x => x.CatalogProductEntityId)
            .ToDictionary(
                g => g.Key,
                g =>
                    g.OrderByDescending(x => x.IsActive)
                        .ThenByDescending(x => x.StartsAt ?? x.CreatedAt)
                        .First()
            );
        var byOffer = products.ToLookup(x => x.CatalogOfferEntityId);

        return new CatalogPageDetail(
            page.Id,
            page.ParentEntityId,
            page.CatalogType.ToString(),
            page.Localization,
            page.Name,
            page.Icon,
            page.Layout,
            [.. page.ImageData ?? []],
            [.. page.TextData ?? []],
            page.Visible,
            [
                .. offers.Select(offer => new CatalogOfferItem(
                    offer.Id,
                    offer.LocalizationId,
                    offer.CostCredits,
                    offer.CostCurrency,
                    offer.CurrencyTypeId,
                    offer.CanGift,
                    offer.CanBundle,
                    offer.ClubLevel,
                    offer.Visible,
                    offer.ClubGiftDaysRequired is not null,
                    offer.ClubGiftDaysRequired,
                    [
                        .. byOffer[offer.Id]
                            .Select(product =>
                            {
                                var definition = product.FurnitureDefinitionEntityId is { } id
                                    ? definitions.TryGetDefinition(id)
                                    : null;
                                var limited = series.GetValueOrDefault(product.Id);

                                return new CatalogProductItem(
                                    product.Id,
                                    TypeName(product.ProductType),
                                    product.FurnitureDefinitionEntityId,
                                    definition?.Name,
                                    definition?.SpriteId,
                                    product.ExtraParam,
                                    product.Quantity,
                                    limited is null
                                        ? null
                                        : new CatalogLimitedItem(
                                            limited.Id,
                                            limited.TotalQuantity,
                                            limited.RemainingQuantity,
                                            limited.RaffleWindowSeconds,
                                            limited.StartsAt,
                                            limited.EndsAt,
                                            limited.IsActive,
                                            limited.IsRaffleFinished
                                        ),
                                    product.SubscriptionType?.ToString(),
                                    product.SubscriptionDays
                                );
                            }),
                    ]
                )),
            ]
        );
    }

    /// <summary>
    /// Furniture an offer can give, for the editor's picker: by class name from its start, or the
    /// one definition with that id.
    /// </summary>
    public CatalogFurnitureItem[] SearchFurniture(string? text)
    {
        var term = (text ?? string.Empty).Trim();

        if (term.Length == 0)
            return [];

        if (int.TryParse(term, out var id))
            return definitions.TryGetDefinition(id) is { } byId ? [Item(byId)] : [];

        return
        [
            .. definitions
                .FindNames(term, Math.Max(1, config.Value.CatalogFurnitureSearchLimit))
                .Select(definitions.TryGetDefinitionByName)
                .OfType<FurnitureDefinitionSnapshot>()
                .Select(Item),
        ];

        static CatalogFurnitureItem Item(FurnitureDefinitionSnapshot x) =>
            new(x.Id, x.Name, x.SpriteId, TypeName(x.ProductType));
    }

    /// <summary>A product type as the panel names it: <c>floor</c>, <c>wall</c>, <c>badge</c>, ...</summary>
    public static string TypeName(ProductType type) =>
        type switch
        {
            ProductType.HabboClub => "club",
            _ => type.ToString().ToLowerInvariant(),
        };

    public static ProductType? TypeOf(string? name) =>
        name?.Trim().ToLowerInvariant() switch
        {
            "floor" => ProductType.Floor,
            "wall" => ProductType.Wall,
            "badge" => ProductType.Badge,
            "effect" => ProductType.Effect,
            "robot" => ProductType.Robot,
            "pet" => ProductType.Pet,
            "club" => ProductType.HabboClub,
            _ => null,
        };

    public static List<string> Lines(string[]? lines) =>
        [.. (lines ?? []).Select(x => x ?? string.Empty)];
}
