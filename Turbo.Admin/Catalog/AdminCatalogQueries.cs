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

    /// <summary>
    /// The name the client opens the club shop by: the toolbar, the club centre, quests and
    /// featured items all open <c>hc_membership</c>, and buying a membership looks it up to buy
    /// from. A page with the club_buy layout and another name is never opened by them.
    /// </summary>
    public const string CLUB_PAGE_NAME = "hc_membership";

    /// <summary>The name the client opens the club gifts by, from the club centre and its notice.</summary>
    public const string CLUB_GIFTS_PAGE_NAME = "club_gifts";

    /// <summary>The layout of the page the catalogue opens on, with the featured items and the voucher box.</summary>
    public const string FRONT_PAGE_LAYOUT = "frontpage4";

    /// <summary>The client's icon of a star, for the front page's tab.</summary>
    public const int FRONT_PAGE_ICON = 64;

    /// <summary>The line over a new front page's voucher box.</summary>
    public const string FRONT_PAGE_VOUCHER_TEXT = "Got a voucher code? Redeem it here.";

    public async Task<CatalogTreeResponse> GetTreeAsync(bool canManage, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var pages = await db
            .CatalogPages.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Localization)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentEntityId,
                x.Localization,
                x.Name,
                x.Icon,
                x.Layout,
                x.Display,
                x.SortOrder,
                Offers = x.Offers!.Count,
            })
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
        var club = await ClubAsync(db, ct).ConfigureAwait(false);

        return new CatalogTreeResponse(
            pages.Where(x => x.ParentEntityId is null).MinBy(x => x.Id)?.Id ?? 0,
            [
                .. pages.Select(x => new CatalogPageNode(
                    x.Id,
                    x.ParentEntityId,
                    x.Localization,
                    x.Name,
                    x.Icon,
                    x.Layout,
                    x.Display.ToName(),
                    x.SortOrder,
                    x.Offers
                )),
            ],
            canManage,
            editor.UnpublishedChanges,
            [
                .. currencies.Select(x =>
                    x.Name.Length > 0 ? x : x with { Name = $"type {x.ActivityPointType}" }
                ),
            ],
            [.. config.Value.CatalogLayouts.Concat(used).Distinct().Order(StringComparer.Ordinal)],
            club
        );
    }

    /// <summary>
    /// The club shop as players reach it: the shown memberships and gifts (the club window lists
    /// them wherever they are), and the shown pages the client opens by name to sell and list
    /// them.
    /// </summary>
    private static async Task<CatalogClubSummary> ClubAsync(TurboDbContext db, CancellationToken ct)
    {
        var normal = db
            .CatalogOffers.AsNoTracking()
            .Where(x => x.Visible && x.Page.Display != CatalogPageDisplay.BuildersClubOnly);
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
                (x.Display == CatalogPageDisplay.Regular || x.Display == CatalogPageDisplay.Both)
                && (x.Name == CLUB_PAGE_NAME || x.Name == CLUB_GIFTS_PAGE_NAME)
            )
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new CatalogClubSummary(
            memberships,
            gifts,
            pages.FirstOrDefault(x => x.Name == CLUB_PAGE_NAME)?.Id,
            pages.FirstOrDefault(x => x.Name == CLUB_GIFTS_PAGE_NAME)?.Id
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
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
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
            page.Localization,
            page.Name,
            page.Icon,
            page.Layout,
            [.. page.ImageData ?? []],
            [.. page.TextData ?? []],
            page.Display.ToName(),
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

    /// <summary>The front page's featured items as saved, in their order.</summary>
    public async Task<CatalogFeaturedResponse> GetFeaturedAsync(CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var items = await db
            .CatalogFeaturedItems.AsNoTracking()
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new CatalogFeaturedResponse([
            .. items.Select(x => new CatalogFeaturedItem(
                x.Id,
                x.Position,
                x.Title,
                x.Image,
                FeaturedTypeName(x.Type),
                x.Value,
                x.ExpiresAt
            )),
        ]);
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

    /// <summary>Whether a definition is a floor or a wall item, which an offer of furni gives; null when it is neither.</summary>
    public ProductType? FurniTypeOf(int definitionId) =>
        definitions.TryGetDefinition(definitionId)?.ProductType is { } type
        && type is ProductType.Floor or ProductType.Wall
            ? type
            : null;

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

    /// <summary>What a featured item opens, as the panel names it: <c>page</c>, <c>offer</c> or <c>product</c>.</summary>
    public static string FeaturedTypeName(CatalogFrontPageItemType type) =>
        type.ToString().ToLowerInvariant();

    public static CatalogFrontPageItemType? FeaturedTypeOf(string? name) =>
        name?.Trim().ToLowerInvariant() switch
        {
            "page" => CatalogFrontPageItemType.Page,
            "offer" => CatalogFrontPageItemType.Offer,
            "product" => CatalogFrontPageItemType.Product,
            _ => null,
        };

    public static List<string> Lines(string[]? lines) =>
        [.. (lines ?? []).Select(x => x ?? string.Empty)];
}
