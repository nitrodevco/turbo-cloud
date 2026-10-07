using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Catalog.Editing;

/// <summary>
/// <see cref="ICatalogEditService"/> over the catalog tables. The catalog is not grain state:
/// the snapshot providers read these rows when they load, so an edit is a write here and goes
/// live on the next <see cref="PublishAsync"/>. Every edit is checked against what the purchase
/// path needs (a furni product names a definition of its type, a currency price names an
/// activity-point currency), and what other rows lean on is not deleted from under them.
/// </summary>
public sealed partial class CatalogEditService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IFurnitureDefinitionProvider definitions,
    ICatalogSnapshotProvider<NormalCatalog> normalCatalog,
    ICatalogSnapshotProvider<BuildersClubCatalog> buildersClubCatalog,
    ISessionGateway sessions,
    IGrainFactory grainFactory,
    ILogger<ICatalogEditService> logger
) : ICatalogEditService
{
    public const int TEXT_MAX_LENGTH = 50;
    public const int LOCALIZATION_ID_MAX_LENGTH = 512;
    public const int EXTRA_PARAM_MAX_LENGTH = 512;
    public const int LAYOUT_SLOTS_MAX = 20;
    public const int LAYOUT_SLOT_MAX_LENGTH = 2000;
    public const int PRICE_MAX = 1_000_000_000;
    public const int QUANTITY_MAX = 100;
    public const int CLUB_LEVEL_MAX = 2;
    public const int SUBSCRIPTION_DAYS_MAX = 3650;
    public const int GIFT_DAYS_MAX = 36500;
    public const int LIMITED_TOTAL_MAX = 1_000_000;
    public const int RAFFLE_WINDOW_MAX = 3600;

    /// <summary>Days in a month of membership, as the club window counts them.</summary>
    private const int DAYS_PER_MONTH = 31;

    private int _unpublished;

    public int UnpublishedChanges => Volatile.Read(ref _unpublished);

    public async Task<CatalogEditResult> CreatePageAsync(
        PlayerId editor,
        int parentId,
        CatalogPageDraft draft,
        CancellationToken ct
    )
    {
        if (CheckPage(draft) is { } refused)
            return refused;

        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var parent = await db
            .CatalogPages.FirstOrDefaultAsync(x => x.Id == parentId, ct)
            .ConfigureAwait(false);

        if (parent is null)
            return CatalogEditResult.Refused("The page to put it under is gone.");

        if (parent.ParentEntityId is null && draft.Display.IsInBuildersClub())
            return TabInBuildersClub;

        var last = await db
            .CatalogPages.Where(x => x.ParentEntityId == parentId)
            .MaxAsync(x => (int?)x.SortOrder, ct)
            .ConfigureAwait(false);
        var page = new CatalogPageEntity
        {
            ParentEntityId = parentId,
            SortOrder = (last ?? -1) + 1,
            Localization = string.Empty,
            Icon = 0,
            Layout = string.Empty,
            Display = draft.Display,
        };

        Apply(page, draft);
        db.CatalogPages.Add(page);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Changed(editor, "created page", page.Id);
    }

    public async Task<CatalogEditResult> UpdatePageAsync(
        PlayerId editor,
        int pageId,
        CatalogPageDraft draft,
        CancellationToken ct
    )
    {
        if (CheckPage(draft) is { } refused)
            return refused;

        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var page = await db
            .CatalogPages.FirstOrDefaultAsync(x => x.Id == pageId, ct)
            .ConfigureAwait(false);

        if (page is null)
            return CatalogEditResult.Refused("That page is gone.");

        if (
            await CheckDisplayAsync(db, page, draft.Display, ct).ConfigureAwait(false) is
            { } refusedDisplay
        )
            return refusedDisplay;

        Apply(page, draft);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Changed(editor, "saved page", pageId);
    }

    public async Task<CatalogEditResult> MovePageAsync(
        PlayerId editor,
        int pageId,
        int parentId,
        int index,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var pages = await db
            .CatalogPages.Select(x => new
            {
                x.Id,
                x.ParentEntityId,
                x.Display,
            })
            .ToDictionaryAsync(x => x.Id, ct)
            .ConfigureAwait(false);

        if (!pages.TryGetValue(pageId, out var page))
            return CatalogEditResult.Refused("That page is gone.");

        if (page.ParentEntityId is null)
            return CatalogEditResult.Refused("The catalog's root stays where it is.");

        if (!pages.TryGetValue(parentId, out var parent))
            return CatalogEditResult.Refused("The page to put it under is gone.");

        if (parent.ParentEntityId is null && page.Display.IsInBuildersClub())
            return TabInBuildersClub;

        // Under itself, or under anything below it, would cut it off from the tree.
        for (int? at = parentId; at is { } id; at = pages[id].ParentEntityId)
        {
            if (id == pageId)
                return CatalogEditResult.Refused("A page can't go under itself.");
        }

        var siblings = await db
            .CatalogPages.Where(x => x.ParentEntityId == parentId && x.Id != pageId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Localization)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var moved = await db.CatalogPages.FirstAsync(x => x.Id == pageId, ct).ConfigureAwait(false);

        moved.ParentEntityId = parentId;
        siblings.Insert(Math.Clamp(index, 0, siblings.Count), moved);

        // Numbered afresh, so the order is the one asked for whatever the old numbers were.
        for (var i = 0; i < siblings.Count; i++)
            siblings[i].SortOrder = i;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Changed(editor, "moved page", pageId);
    }

    public async Task<CatalogEditResult> DeletePageAsync(
        PlayerId editor,
        int pageId,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var page = await db
            .CatalogPages.FirstOrDefaultAsync(x => x.Id == pageId, ct)
            .ConfigureAwait(false);

        if (page is null)
            return CatalogEditResult.Refused("That page is gone.");

        if (page.ParentEntityId is null)
            return CatalogEditResult.Refused("The catalog's root can't be deleted.");

        if (
            await db
                .CatalogPages.AnyAsync(x => x.ParentEntityId == pageId, ct)
                .ConfigureAwait(false)
        )
            return CatalogEditResult.Refused("Move or delete the pages under it first.");

        // Deleting the page would delete its offers with it, and with them whatever was bought.
        if (
            await db
                .CatalogOffers.AnyAsync(x => x.CatalogPageEntityId == pageId, ct)
                .ConfigureAwait(false)
        )
            return CatalogEditResult.Refused("Move or delete its offers first.");

        db.CatalogPages.Remove(page);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Changed(editor, "deleted page", pageId);
    }

    public async Task<CatalogEditResult> CreateOfferAsync(
        PlayerId editor,
        CatalogOfferDraft draft,
        CancellationToken ct
    )
    {
        if (draft.Product is null)
            return CatalogEditResult.Refused("Say what the offer gives.");

        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        if (await CheckOfferAsync(db, draft, null, ct).ConfigureAwait(false) is { } refused)
            return refused;

        var offer = new CatalogOfferEntity
        {
            CatalogPageEntityId = draft.PageId,
            LocalizationId = string.Empty,
            CostCredits = 0,
            CostCurrency = 0,
            CanGift = true,
            CanBundle = true,
            ClubLevel = 0,
            Visible = true,
            Page = null!,
        };
        var product = new CatalogProductEntity
        {
            CatalogOfferEntityId = 0,
            ProductType = draft.Product.Type,
            Quantity = 1,
            Offer = offer,
        };

        Apply(offer, draft, IsMembership(draft, null));
        Apply(product, draft.Product);
        db.CatalogOffers.Add(offer);
        db.CatalogProducts.Add(product);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Changed(editor, "created offer", offer.Id);
    }

    public async Task<CatalogEditResult> UpdateOfferAsync(
        PlayerId editor,
        int offerId,
        CatalogOfferDraft draft,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var offer = await db
            .CatalogOffers.Include(x => x.Page)
            .Include(x => x.Products)
            .FirstOrDefaultAsync(x => x.Id == offerId, ct)
            .ConfigureAwait(false);

        if (offer is null)
            return CatalogEditResult.Refused("That offer is gone.");

        if (await CheckOfferAsync(db, draft, offer, ct).ConfigureAwait(false) is { } refused)
            return refused;

        if (draft.Product is { } productDraft)
        {
            var products = offer.Products ?? [];

            if (products.Count != 1)
                return CatalogEditResult.Refused(
                    "This offer gives several things; what it gives can't be changed here."
                );

            var product = products[0];

            // A limited series is sold as one item: swapping the item would sell the rest of
            // the series as something else.
            if (
                productDraft.DefinitionId != product.FurnitureDefinitionEntityId
                && await db
                    .LtdSeries.AnyAsync(x => x.CatalogProductEntityId == product.Id, ct)
                    .ConfigureAwait(false)
            )
                return CatalogEditResult.Refused(
                    "This offer sells a limited series; the item it gives can't be changed."
                );

            Apply(product, productDraft);
        }

        Apply(offer, draft, IsMembership(draft, offer));
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Changed(editor, "saved offer", offerId);
    }

    public async Task<CatalogEditResult> DeleteOfferAsync(
        PlayerId editor,
        int offerId,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var offer = await db
            .CatalogOffers.FirstOrDefaultAsync(x => x.Id == offerId, ct)
            .ConfigureAwait(false);

        if (offer is null)
            return CatalogEditResult.Refused("That offer is gone.");

        if (offer.ClubGiftDaysRequired is not null)
            return CatalogEditResult.Refused(
                "This is a club gift members claim; hide it instead of deleting it."
            );

        if (
            await db
                .LtdSeries.AnyAsync(x => x.CatalogProduct!.CatalogOfferEntityId == offerId, ct)
                .ConfigureAwait(false)
        )
            return CatalogEditResult.Refused(
                "This offer sells a limited series, which would go with it; hide it instead."
            );

        // Builders Club furni stays tied to the offer it was placed from.
        var placed = await db
            .BuildersClubFurnitures.CountAsync(x => x.CatalogOfferEntityId == offerId, ct)
            .ConfigureAwait(false);

        if (placed > 0)
            return CatalogEditResult.Refused(
                $"{placed} Builders Club items were placed from this offer; hide it instead."
            );

        db.CatalogOffers.Remove(offer);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Changed(editor, "deleted offer", offerId);
    }

    public async Task<CatalogPublishResult> PublishAsync(PlayerId editor, CancellationToken ct)
    {
        var published = Interlocked.Exchange(ref _unpublished, 0);

        await normalCatalog.ReloadAsync(ct).ConfigureAwait(false);
        await buildersClubCatalog.ReloadAsync(ct).ConfigureAwait(false);

        var online = sessions.GetOnlinePlayerIds();

        await grainFactory
            .SendComposerToPlayersAsync(online, new CatalogPublishedMessageComposer(), ct)
            .ConfigureAwait(false);

        var normal = normalCatalog.Current;
        var buildersClub = buildersClubCatalog.Current;

        logger.LogInformation(
            "Player {PlayerId} published the catalog ({Changes} changes), told {Players} players",
            editor,
            published,
            online.Count
        );

        return new CatalogPublishResult(
            normal.PagesById.Count + buildersClub.PagesById.Count,
            normal.OffersById.Count + buildersClub.OffersById.Count,
            online.Count
        );
    }

    public async Task<CatalogEditResult> SaveLimitedAsync(
        PlayerId editor,
        int offerId,
        CatalogLimitedDraft draft,
        CancellationToken ct
    )
    {
        if (draft.TotalQuantity is < 1 or > LIMITED_TOTAL_MAX)
            return CatalogEditResult.Refused(
                $"A limited series is 1 to {LIMITED_TOTAL_MAX:N0} items."
            );

        if (draft.RaffleWindowSeconds is < 0 or > RAFFLE_WINDOW_MAX)
            return CatalogEditResult.Refused(
                $"The raffle gathers buyers for 0 to {RAFFLE_WINDOW_MAX} seconds."
            );

        if (draft.StartsAtUtc is { } starts && draft.EndsAtUtc is { } ends && ends <= starts)
            return CatalogEditResult.Refused("The sale ends after it starts.");

        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var offer = await db
            .CatalogOffers.Include(x => x.Page)
            .Include(x => x.Products)
            .FirstOrDefaultAsync(x => x.Id == offerId, ct)
            .ConfigureAwait(false);

        if (offer is null)
            return CatalogEditResult.Refused("That offer is gone.");

        // The raffle, the purchase that routes to it and the upcoming-LTD notice all read the
        // normal catalog.
        if (!offer.Page.Display.IsIn(CatalogType.Normal))
            return CatalogEditResult.Refused("Limited series are sold from the normal catalog.");

        if (offer.ClubGiftDaysRequired is not null)
            return CatalogEditResult.Refused("A club gift can't be a limited series.");

        if (
            offer.Products is not [var product]
            || product.ProductType is not (ProductType.Floor or ProductType.Wall)
        )
            return CatalogEditResult.Refused("A limited series sells one floor or wall item.");

        var series = await SeriesOfAsync(db, product.Id, ct).ConfigureAwait(false);

        if (series is null)
        {
            db.LtdSeries.Add(
                new LtdSeriesEntity
                {
                    CatalogProductEntityId = product.Id,
                    TotalQuantity = draft.TotalQuantity,
                    RemainingQuantity = draft.TotalQuantity,
                    RaffleWindowSeconds = draft.RaffleWindowSeconds,
                    IsActive = draft.Active,
                    StartsAt = draft.StartsAtUtc,
                    EndsAt = draft.EndsAtUtc,
                }
            );
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return Changed(editor, "made limited offer", offerId);
        }

        var total = draft.TotalQuantity;
        var delta = total - series.TotalQuantity;

        // One statement against the row as it is, not as it was read: the raffle sells from it
        // at any moment, and what is left moves by as much as the total, so no serial is lost
        // or given twice. Each column is set from its own old value only, as MySQL reads back
        // what a statement has already set; the total being the one read keeps the step right.
        var saved = await db
            .LtdSeries.Where(x =>
                x.Id == series.Id
                && x.TotalQuantity == series.TotalQuantity
                && x.TotalQuantity - x.RemainingQuantity <= total
            )
            .ExecuteUpdateAsync(
                set =>
                    set.SetProperty(x => x.RemainingQuantity, x => x.RemainingQuantity + delta)
                        .SetProperty(x => x.TotalQuantity, total)
                        .SetProperty(x => x.RaffleWindowSeconds, draft.RaffleWindowSeconds)
                        .SetProperty(x => x.IsActive, draft.Active)
                        .SetProperty(x => x.StartsAt, draft.StartsAtUtc)
                        .SetProperty(x => x.EndsAt, draft.EndsAtUtc),
                ct
            )
            .ConfigureAwait(false);

        if (saved == 0)
        {
            var now = await SeriesOfAsync(db, product.Id, ct).ConfigureAwait(false);

            return now is not null && now.TotalQuantity != series.TotalQuantity
                ? CatalogEditResult.Refused("The series changed meanwhile; look again and retry.")
                : CatalogEditResult.Refused(
                    $"{(now ?? series).TotalQuantity - (now ?? series).RemainingQuantity} of it are sold already; the series can't be smaller than that."
                );
        }

        // An open raffle holds the series it read; it reads it again now, so a series switched
        // off or ended stops selling without waiting for its next draw.
        await grainFactory.GetLtdRaffleGrain(series.Id).ReloadSeriesAsync(ct).ConfigureAwait(false);

        return Changed(editor, "saved the limited series of offer", offerId);
    }

    public async Task<CatalogEditResult> RemoveLimitedAsync(
        PlayerId editor,
        int offerId,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var productIds = await db
            .CatalogProducts.Where(x => x.CatalogOfferEntityId == offerId)
            .Select(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var series = await db
            .LtdSeries.Where(x => productIds.Contains(x.CatalogProductEntityId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (series.Count == 0)
            return CatalogEditResult.Refused("That offer is not a limited series.");

        var sold = series.Sum(x => x.TotalQuantity - x.RemainingQuantity);
        var seriesIds = series.Select(x => x.Id).ToList();

        // The serials handed out, and the raffle's record of who entered, belong to the series.
        if (
            sold > 0
            || await db
                .LtdRaffleEntries.AnyAsync(x => seriesIds.Contains(x.SeriesEntityId), ct)
                .ConfigureAwait(false)
        )
            return CatalogEditResult.Refused(
                "Some of it is sold or raffled already; switch the series off instead."
            );

        db.LtdSeries.RemoveRange(series);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Changed(editor, "took the limited series off offer", offerId);
    }

    /// <summary>The series a product sells, as the catalog picks it: the active one, else the newest.</summary>
    private static async Task<LtdSeriesEntity?> SeriesOfAsync(
        TurboDbContext db,
        int productId,
        CancellationToken ct
    ) =>
        (
            await db
                .LtdSeries.AsNoTracking()
                .Where(x => x.CatalogProductEntityId == productId)
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.StartsAt ?? x.CreatedAt)
            .FirstOrDefault();

    /// <summary>Whether the offer gives a membership, as saved or as it will be.</summary>
    private static bool IsMembership(CatalogOfferDraft draft, CatalogOfferEntity? existing) =>
        draft.Product is { } product
            ? product.Type == ProductType.HabboClub
            : existing?.Products?.Any(x => x.SubscriptionType is not null) == true;

    /// <summary>An edit saved: one more change to publish, and a line in the log saying who.</summary>
    private CatalogEditResult Changed(PlayerId editor, string change, int id)
    {
        Interlocked.Increment(ref _unpublished);
        logger.LogInformation("Player {PlayerId} {Change} {Id} in the catalog", editor, change, id);

        return CatalogEditResult.Done(id);
    }

    /// <summary>
    /// The Builders Club catalog has no tabs, so a tab can't be shown there; the pages under it
    /// can.
    /// </summary>
    private static CatalogEditResult TabInBuildersClub =>
        CatalogEditResult.Refused(
            "Tabs aren't shown in the Builders Club catalog; show the pages under it there instead."
        );

    /// <summary>
    /// Whether a saved page may be shown as asked: not in the Builders Club catalog when it is a
    /// tab, and not out of the normal catalog while it sells what only that catalog sells.
    /// </summary>
    private static async Task<CatalogEditResult?> CheckDisplayAsync(
        TurboDbContext db,
        CatalogPageEntity page,
        CatalogPageDisplay display,
        CancellationToken ct
    )
    {
        if (display == page.Display || page.ParentEntityId is not { } parentId)
            return null;

        if (
            display.IsInBuildersClub()
            && await db
                .CatalogPages.AnyAsync(x => x.Id == parentId && x.ParentEntityId == null, ct)
                .ConfigureAwait(false)
        )
            return TabInBuildersClub;

        if (display.IsIn(CatalogType.Normal))
            return null;

        // Memberships, club gifts and limited series are read from the normal catalog.
        var normalOnly = await db
            .CatalogOffers.AnyAsync(
                x =>
                    x.CatalogPageEntityId == page.Id
                    && (
                        x.ClubGiftDaysRequired != null
                        || x.Products!.Any(p => p.SubscriptionType != null)
                        || db.LtdSeries.Any(l => l.CatalogProduct!.CatalogOfferEntityId == x.Id)
                    ),
                ct
            )
            .ConfigureAwait(false);

        return normalOnly
            ? CatalogEditResult.Refused(
                "This page sells memberships, club gifts or limited series, which only the normal catalog sells; move them first."
            )
            : null;
    }

    private static CatalogEditResult? CheckPage(CatalogPageDraft draft)
    {
        var title = draft.Localization.Trim();

        if (title.Length == 0 || title.Length > TEXT_MAX_LENGTH)
            return CatalogEditResult.Refused(
                $"Give the page a title of up to {TEXT_MAX_LENGTH} characters."
            );

        if ((draft.Name?.Trim().Length ?? 0) > TEXT_MAX_LENGTH)
            return CatalogEditResult.Refused(
                $"The page's name is up to {TEXT_MAX_LENGTH} characters."
            );

        if (draft.Name is { } name && name.Trim().Length > 0 && !KeyPattern().IsMatch(name.Trim()))
            return CatalogEditResult.Refused("The page's name is letters, digits, _ and - only.");

        if (
            draft.Layout.Trim().Length is 0 or > TEXT_MAX_LENGTH
            || !KeyPattern().IsMatch(draft.Layout.Trim())
        )
            return CatalogEditResult.Refused("Choose a layout.");

        if (draft.Icon < 0)
            return CatalogEditResult.Refused("The icon is a number, 0 for none.");

        if (
            draft.ImageData.Count > LAYOUT_SLOTS_MAX
            || draft.TextData.Count > LAYOUT_SLOTS_MAX
            || draft.ImageData.Concat(draft.TextData).Any(x => x.Length > LAYOUT_SLOT_MAX_LENGTH)
        )
            return CatalogEditResult.Refused(
                $"A page has up to {LAYOUT_SLOTS_MAX} images and texts, each up to {LAYOUT_SLOT_MAX_LENGTH} characters."
            );

        return null;
    }

    private async Task<CatalogEditResult?> CheckOfferAsync(
        TurboDbContext db,
        CatalogOfferDraft draft,
        CatalogOfferEntity? existing,
        CancellationToken ct
    )
    {
        var display = await db
            .CatalogPages.Where(x => x.Id == draft.PageId)
            .Select(x => (CatalogPageDisplay?)x.Display)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (display is null)
            return CatalogEditResult.Refused("The page to put it on is gone.");

        var normal = display.Value.IsIn(CatalogType.Normal);
        var membership = IsMembership(draft, existing);

        if (draft.ClubGiftDaysRequired is { } giftDays)
        {
            if (
                await CheckGiftAsync(db, draft, existing, normal, giftDays, ct)
                    .ConfigureAwait(false) is
                { } refusedGift
            )
                return refusedGift;
        }

        // The club window and its renewals read the normal catalog, and a member can't buy what
        // asks for membership first.
        if (membership && !normal)
            return CatalogEditResult.Refused("Memberships are sold from the normal catalog.");

        if (membership && draft.ClubLevel != 0)
            return CatalogEditResult.Refused(
                "A membership is for anyone; asking for club first would keep non-members out."
            );

        if (draft.LocalizationId.Trim().Length > LOCALIZATION_ID_MAX_LENGTH)
            return CatalogEditResult.Refused(
                $"The name key is up to {LOCALIZATION_ID_MAX_LENGTH} characters."
            );

        if (draft.CostCredits is < 0 or > PRICE_MAX || draft.CostCurrency is < 0 or > PRICE_MAX)
            return CatalogEditResult.Refused("A price is 0 or more.");

        if (draft.ClubLevel is < 0 or > CLUB_LEVEL_MAX)
            return CatalogEditResult.Refused("The club level is none, club or VIP.");

        // The wallet charges an activity-point currency by its type; a row that has none (credits,
        // silver, emeralds) would charge nothing.
        if (draft.CostCurrency > 0)
        {
            if (draft.CurrencyTypeId is not { } currencyId)
                return CatalogEditResult.Refused("Say which currency the second price is in.");

            if (
                !await db
                    .CurrencyTypes.AnyAsync(
                        x => x.Id == currencyId && x.ActivityPointType != null && x.Enabled,
                        ct
                    )
                    .ConfigureAwait(false)
            )
                return CatalogEditResult.Refused(
                    "That currency is not an activity-point currency the hotel charges."
                );
        }

        if (draft.Product is not { } product)
            return null;

        if (product.Quantity is < 1 or > QUANTITY_MAX)
            return CatalogEditResult.Refused($"An offer gives 1 to {QUANTITY_MAX} of its item.");

        if ((product.ExtraParam?.Length ?? 0) > EXTRA_PARAM_MAX_LENGTH)
            return CatalogEditResult.Refused(
                $"The extra parameter is up to {EXTRA_PARAM_MAX_LENGTH} characters."
            );

        switch (product.Type)
        {
            case ProductType.Floor:
            case ProductType.Wall:
                // The inventory hands out the definition; one of the other type, or none, fails
                // every purchase and refunds it.
                if (
                    product.DefinitionId is not { } definitionId
                    || definitions.TryGetDefinition(definitionId) is not { } definition
                )
                    return CatalogEditResult.Refused("Choose the item it gives.");

                if (definition.ProductType != product.Type)
                    return CatalogEditResult.Refused(
                        product.Type == ProductType.Floor
                            ? $"{definition.Name} is a wall item."
                            : $"{definition.Name} is a floor item."
                    );

                return null;
            case ProductType.Badge:
                return string.IsNullOrWhiteSpace(product.ExtraParam)
                    ? CatalogEditResult.Refused("Give the badge code.")
                    : null;
            // Days of a membership, granted when it is bought; no item comes with it.
            case ProductType.HabboClub:
                if (product.Subscription is null)
                    return CatalogEditResult.Refused("Say which membership it gives.");

                if (product.SubscriptionDays is < 1 or > SUBSCRIPTION_DAYS_MAX)
                    return CatalogEditResult.Refused(
                        $"A membership is 1 to {SUBSCRIPTION_DAYS_MAX} days."
                    );

                // Each purchase is one extension: a quantity would multiply the days but count
                // as a single period.
                return product.Quantity != 1
                    ? CatalogEditResult.Refused("A membership is bought one at a time.")
                    : null;
            default:
                return CatalogEditResult.Refused(
                    "Only floor items, wall items, badges and memberships can be set up here."
                );
        }
    }

    /// <summary>
    /// A club gift is claimed by its name key and given by the inventory, which hands out furni;
    /// a membership or a badge in a gift would be claimed for nothing.
    /// </summary>
    private async Task<CatalogEditResult?> CheckGiftAsync(
        TurboDbContext db,
        CatalogOfferDraft draft,
        CatalogOfferEntity? existing,
        bool normal,
        int giftDays,
        CancellationToken ct
    )
    {
        if (!normal)
            return CatalogEditResult.Refused("Club gifts are in the normal catalog.");

        if (giftDays is < 0 or > GIFT_DAYS_MAX)
            return CatalogEditResult.Refused(
                $"A club gift needs 0 to {GIFT_DAYS_MAX} days of club."
            );

        var gives = draft.Product is { } product
            ? [product.Type]
            : existing?.Products?.Select(x => x.ProductType).ToList() ?? [];

        if (
            gives.Count == 0
            || gives.Any(x => x is not (ProductType.Floor or ProductType.Wall or ProductType.Robot))
        )
            return CatalogEditResult.Refused("A club gift gives furni.");

        if (
            existing?.Products is { Count: > 0 } products
            && await db
                .LtdSeries.AnyAsync(
                    x => products.Select(p => p.Id).Contains(x.CatalogProductEntityId),
                    ct
                )
                .ConfigureAwait(false)
        )
            return CatalogEditResult.Refused("A limited series can't be a club gift.");

        var name = NameOf(draft, existing);
        var existingId = existing?.Id ?? 0;

        return await db
            .CatalogOffers.AnyAsync(
                x =>
                    x.Id != existingId
                    && x.ClubGiftDaysRequired != null
                    && x.LocalizationId == name,
                ct
            )
            .ConfigureAwait(false)
            ? CatalogEditResult.Refused(
                $"Another club gift is called {name}; members claim a gift by its name key, so give this one its own."
            )
            : null;
    }

    /// <summary>
    /// The name key an offer is saved with: the one given, else what it gives (the item's class
    /// name, or a membership's length), else the one it has.
    /// </summary>
    private string NameOf(CatalogOfferDraft draft, CatalogOfferEntity? existing)
    {
        var name = draft.LocalizationId.Trim();

        if (name.Length > 0)
            return name;

        if (draft.Product?.DefinitionId is { } definitionId)
            name = definitions.TryGetDefinition(definitionId)?.Name ?? string.Empty;
        else if (draft.Product is { Type: ProductType.HabboClub, SubscriptionDays: > 0 } club)
            name = MembershipName(club.Subscription, club.SubscriptionDays);

        return name.Length > 0 ? name : existing?.LocalizationId ?? string.Empty;
    }

    /// <summary>
    /// A membership's name key by its length, as the hotel's own are: <c>habbo_club_3_months</c>,
    /// or in days when it is not whole months.
    /// </summary>
    public static string MembershipName(SubscriptionType? type, int days)
    {
        var prefix = type == SubscriptionType.BuildersClub ? "builders_club" : "habbo_club";

        return days % DAYS_PER_MONTH == 0
            ? $"{prefix}_{days / DAYS_PER_MONTH}_{(days == DAYS_PER_MONTH ? "month" : "months")}"
            : $"{prefix}_{days}_days";
    }

    private static void Apply(CatalogPageEntity page, CatalogPageDraft draft)
    {
        page.Localization = draft.Localization.Trim();
        page.Name = string.IsNullOrWhiteSpace(draft.Name) ? null : draft.Name.Trim();
        page.Icon = draft.Icon;
        page.Layout = draft.Layout.Trim();
        page.ImageData = [.. draft.ImageData];
        page.TextData = [.. draft.TextData];
        page.Display = draft.Display;
    }

    private void Apply(CatalogOfferEntity offer, CatalogOfferDraft draft, bool membership)
    {
        // An offer named by what it gives, as the hotel's own offers are.
        var name = NameOf(draft, offer);

        if (name.Length > 0)
            offer.LocalizationId = name;

        offer.CatalogPageEntityId = draft.PageId;
        offer.CostCredits = draft.CostCredits;
        offer.CostCurrency = draft.CostCurrency;
        offer.CurrencyTypeId = draft.CostCurrency > 0 ? draft.CurrencyTypeId : null;
        // A membership has no gift path yet, and buying several at once is one period.
        offer.CanGift = draft.CanGift && !membership;
        offer.CanBundle = draft.CanBundle && !membership;
        offer.ClubLevel = draft.ClubLevel;
        offer.Visible = draft.Visible;
        offer.ClubGiftDaysRequired = draft.ClubGiftDaysRequired;
    }

    private static void Apply(CatalogProductEntity product, CatalogProductDraft draft)
    {
        product.ProductType = draft.Type;
        product.FurnitureDefinitionEntityId = draft.Type is ProductType.Floor or ProductType.Wall
            ? draft.DefinitionId
            : null;
        product.ExtraParam = string.IsNullOrWhiteSpace(draft.ExtraParam)
            ? null
            : draft.ExtraParam.Trim();
        product.Quantity = draft.Quantity;
        product.SubscriptionType = draft.Type == ProductType.HabboClub ? draft.Subscription : null;
        product.SubscriptionDays = draft.Type == ProductType.HabboClub ? draft.SubscriptionDays : 0;
    }

    /// <summary>A key the client looks a page or layout up by.</summary>
    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex KeyPattern();
}
