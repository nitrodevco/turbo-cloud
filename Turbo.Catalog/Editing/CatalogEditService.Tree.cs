using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Catalog.Editing;

public sealed partial class CatalogEditService
{
    /// <summary>The most a tree draft makes or moves at once.</summary>
    public const int TREE_ROWS_MAX = 50_000;

    public async Task<CatalogTreeResult> BuildTreeAsync(
        PlayerId editor,
        string label,
        CatalogTreeDraft draft,
        CancellationToken ct
    )
    {
        if (
            draft.Pages.Count + draft.Offers.Count + draft.OfferMoves.Count + draft.PageMoves.Count
            > TREE_ROWS_MAX
        )
            return CatalogTreeResult.Refused(
                $"That is more than {TREE_ROWS_MAX:N0} changes at once; do it in parts."
            );

        if (CheckTreeOffers(draft) is { } refusedOffer)
            return CatalogTreeResult.Refused(refusedOffer);

        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var saved = await db
            .CatalogPages.Select(x => new
            {
                x.Id,
                x.ParentEntityId,
                x.Display,
            })
            .ToDictionaryAsync(x => x.Id, ct)
            .ConfigureAwait(false);

        // What each page is - saved or new - and where it sits, to check the tree as it will be.
        var made = new List<CatalogPageEntity>(draft.Pages.Count);
        var parents = new List<(int? Saved, int? New)>(draft.Pages.Count);

        for (var i = 0; i < draft.Pages.Count; i++)
        {
            var (parentRef, page, _) = draft.Pages[i];

            if (CheckPage(page) is { } refusedPage)
                return CatalogTreeResult.Refused($"{page.Localization}: {refusedPage.Error}");

            if (Resolve(parentRef, i) is not { } parent)
                return CatalogTreeResult.Refused(
                    $"{page.Localization}: the page to put it under is not in the tree."
                );

            if (parent.Saved is { } savedId && !saved.ContainsKey(savedId))
                return CatalogTreeResult.Refused(
                    $"{page.Localization}: the page to put it under is gone."
                );

            if (IsRoot(parent) && page.Display.IsInBuildersClub())
                return CatalogTreeResult.Refused($"{page.Localization}: {TabInBuildersClub.Error}");

            parents.Add(parent);
            made.Add(
                new CatalogPageEntity
                {
                    Localization = string.Empty,
                    Icon = 0,
                    Layout = string.Empty,
                    SortOrder = 0,
                    Display = page.Display,
                }
            );
            Apply(made[i], page);
        }

        foreach (var move in draft.PageMoves)
        {
            if (!saved.TryGetValue(move.PageId, out var movingPage))
                return CatalogTreeResult.Refused($"Page {move.PageId} is gone.");

            if (movingPage.ParentEntityId is null)
                return CatalogTreeResult.Refused("The catalog's root stays where it is.");

            if (Resolve(move.Parent, made.Count) is not { } parent)
                return CatalogTreeResult.Refused("A page is moved under a page not in the tree.");

            if (IsRoot(parent) && movingPage.Display.IsInBuildersClub())
                return CatalogTreeResult.Refused(TabInBuildersClub.Error!);

            if (Above(parent).Contains(move.PageId))
                return CatalogTreeResult.Refused("A page can't go under itself.");
        }

        var displays = new Dictionary<CatalogPageRef, CatalogPageDisplay>();

        foreach (
            var target in draft
                .Offers.Select(x => x.Page)
                .Concat(draft.OfferMoves.Select(x => x.Page))
                .Distinct()
        )
        {
            if (Resolve(target, made.Count) is not { } page)
                return CatalogTreeResult.Refused("An offer goes on a page not in the tree.");

            if (page.Saved is { } savedId && !saved.ContainsKey(savedId))
                return CatalogTreeResult.Refused($"Page {savedId} is gone.");

            displays[target] = page.Saved is { } id
                ? saved[id].Display
                : made[page.New!.Value].Display;
        }

        if (
            await CheckTreeCurrenciesAsync(db, draft, ct).ConfigureAwait(false) is
            { } refusedCurrency
        )
            return CatalogTreeResult.Refused(refusedCurrency);

        var moving = draft.OfferMoves.Select(x => x.OfferId).Distinct().ToList();
        var offers = await LoadInChunksAsync(
                moving,
                ids => db.CatalogOffers.Include(x => x.Products).Where(x => ids.Contains(x.Id)),
                ct
            )
            .ConfigureAwait(false);

        if (offers.Count != moving.Count)
            return CatalogTreeResult.Refused("An offer to move is gone.");

        var byId = offers.ToDictionary(x => x.Id);
        var limited = (
            await LoadInChunksAsync(
                    [.. offers.SelectMany(x => x.Products ?? []).Select(x => x.Id)],
                    ids =>
                        db.LtdSeries.Where(x => ids.Contains(x.CatalogProductEntityId))
                            .Select(x => x.CatalogProduct!.CatalogOfferEntityId),
                    ct
                )
                .ConfigureAwait(false)
        ).ToHashSet();

        foreach (var move in draft.OfferMoves)
        {
            var offer = byId[move.OfferId];

            if (displays[move.Page].IsIn(CatalogType.Normal))
                continue;

            if (
                offer.ClubGiftDaysRequired is not null
                || offer.Products?.Any(x => x.SubscriptionType is not null) == true
                || limited.Contains(offer.Id)
            )
                return CatalogTreeResult.Refused(
                    $"Offer {offer.Id} sells what only the normal catalog sells; it can't go on a page only the Builders Club shows."
                );
        }

        // The pages: made under their parents, each new page last among them unless placed.
        var lastPlace = await db
            .CatalogPages.Where(x => x.ParentEntityId != null)
            .GroupBy(x => x.ParentEntityId!.Value)
            .Select(g => new { g.Key, Last = g.Max(x => x.SortOrder) })
            .ToDictionaryAsync(x => x.Key, x => x.Last, ct)
            .ConfigureAwait(false);
        var newPlaces = new Dictionary<int, int>();

        for (var i = 0; i < made.Count; i++)
        {
            var page = made[i];
            var parent = parents[i];

            if (parent.Saved is { } parentId)
            {
                page.ParentEntityId = parentId;

                if (draft.Pages[i].Index is { } index)
                    page.SortOrder = await PlaceAmongAsync(db, parentId, index, ct)
                        .ConfigureAwait(false);
                else
                {
                    page.SortOrder = lastPlace.GetValueOrDefault(parentId, -1) + 1;
                    lastPlace[parentId] = page.SortOrder;
                }
            }
            else
            {
                var parentIndex = parent.New!.Value;

                page.ParentEntity = made[parentIndex];
                page.SortOrder = newPlaces.GetValueOrDefault(parentIndex);
                newPlaces[parentIndex] = page.SortOrder + 1;
            }

            db.CatalogPages.Add(page);
        }

        if (draft.PageMoves.Count > 0)
        {
            var moved = await LoadInChunksAsync(
                    [.. draft.PageMoves.Select(x => x.PageId).Distinct()],
                    ids => db.CatalogPages.Where(x => ids.Contains(x.Id)),
                    ct
                )
                .ConfigureAwait(false);
            var movedById = moved.ToDictionary(x => x.Id);

            foreach (var move in draft.PageMoves)
            {
                var page = movedById[move.PageId];
                var parent = Resolve(move.Parent, made.Count)!.Value;

                if (parent.Saved is { } parentId)
                {
                    page.ParentEntityId = parentId;
                    page.SortOrder = lastPlace.GetValueOrDefault(parentId, -1) + 1;
                    lastPlace[parentId] = page.SortOrder;
                }
                else
                {
                    var parentIndex = parent.New!.Value;

                    page.ParentEntity = made[parentIndex];
                    page.SortOrder = newPlaces.GetValueOrDefault(parentIndex);
                    newPlaces[parentIndex] = page.SortOrder + 1;
                }
            }
        }

        // The offers: each last on its page, the new ones before the moved, in the order given.
        var savedTargets = displays
            .Keys.Where(x => x.Id is not null)
            .Select(x => x.Id!.Value)
            .ToList();
        var offerPlaces = (
            await LoadInChunksAsync(
                    savedTargets,
                    ids =>
                        db.CatalogOffers.Where(x => ids.Contains(x.CatalogPageEntityId))
                            .GroupBy(x => x.CatalogPageEntityId)
                            .Select(g => new { g.Key, Last = g.Max(x => x.SortOrder) }),
                    ct
                )
                .ConfigureAwait(false)
        ).ToDictionary(x => CatalogPageRef.Saved(x.Key), x => x.Last + 1);

        int NextPlace(CatalogPageRef page)
        {
            var place = offerPlaces.GetValueOrDefault(page);

            offerPlaces[page] = place + 1;

            return place;
        }

        foreach (var (pageRef, offerDraft) in draft.Offers)
        {
            var offer = new CatalogOfferEntity
            {
                CatalogPageEntityId = pageRef.Id ?? 0,
                LocalizationId = string.Empty,
                CostCredits = 0,
                CostCurrency = 0,
                CanGift = true,
                CanBundle = true,
                ClubLevel = 0,
                Visible = true,
                SortOrder = NextPlace(pageRef),
                Page = null!,
            };

            Apply(offer, offerDraft with { PageId = pageRef.Id ?? 0 }, null);

            if (pageRef.New is { } newPage)
                offer.Page = made[newPage];

            db.CatalogOffers.Add(offer);

            foreach (var product in Gives(offerDraft)!)
                db.CatalogProducts.Add(NewProduct(offer, product));
        }

        foreach (var move in draft.OfferMoves)
        {
            var offer = byId[move.OfferId];

            if (move.Page.Id is { } pageId)
                offer.CatalogPageEntityId = pageId;
            else
                offer.Page = made[move.Page.New!.Value];

            offer.SortOrder = NextPlace(move.Page);
        }

        var changes = await SaveAsync(db, ct).ConfigureAwait(false);

        Changed(editor, label, 0, changes);

        return new CatalogTreeResult(
            null,
            draft.Offers.Count,
            draft.OfferMoves.Count,
            draft.PageMoves.Count,
            [.. made.Select(x => x.Id)]
        );

        // A reference as the page it is: saved, or new and made before what refers to it.
        (int? Saved, int? New)? Resolve(CatalogPageRef page, int madeSoFar) =>
            page switch
            {
                { Id: { } id, New: null } => (id, null),
                { Id: null, New: { } index } when index >= 0 && index < madeSoFar => (null, index),
                _ => null,
            };

        bool IsRoot((int? Saved, int? New) page) =>
            page.Saved is { } id
            && saved.TryGetValue(id, out var row)
            && row.ParentEntityId is null;

        // The saved pages above a page, itself included when it is saved.
        HashSet<int> Above((int? Saved, int? New) page)
        {
            var above = new HashSet<int>();

            while (page.New is { } index)
                page = parents[index];

            for (
                int? at = page.Saved;
                at is { } id && above.Add(id);
                at = saved.GetValueOrDefault(id)?.ParentEntityId
            ) { }

            return above;
        }
    }

    /// <summary>
    /// The new offers checked as one by hand is, but for what needs the database: what each gives,
    /// its name key, prices and club level. Memberships and club gifts are made one at a time.
    /// </summary>
    private string? CheckTreeOffers(CatalogTreeDraft draft)
    {
        foreach (var (_, offer) in draft.Offers)
        {
            if (Gives(offer) is not { } gives)
                return "Say what each offer gives.";

            if (
                offer.ClubGiftDaysRequired is not null
                || gives.Any(x => x.Type == ProductType.HabboClub)
            )
                return "Memberships and club gifts are made one at a time.";

            if (CheckProducts(gives) is { } refused)
                return $"{offer.LocalizationId}: {refused.Error}";

            if (offer.LocalizationId.Trim().Length > LOCALIZATION_ID_MAX_LENGTH)
                return $"The name key is up to {LOCALIZATION_ID_MAX_LENGTH} characters.";

            if (offer.CostCredits is < 0 or > PRICE_MAX || offer.CostCurrency is < 0 or > PRICE_MAX)
                return "A price is 0 or more.";

            if (offer.ClubLevel is < 0 or > CLUB_LEVEL_MAX)
                return "The club level is none, club or VIP.";

            if (offer.CostCurrency > 0 && offer.CurrencyTypeId is null)
                return "Say which currency the second price is in.";
        }

        return null;
    }

    private static async Task<string?> CheckTreeCurrenciesAsync(
        TurboDbContext db,
        CatalogTreeDraft draft,
        CancellationToken ct
    )
    {
        var wanted = draft
            .Offers.Where(x => x.Offer.CostCurrency > 0)
            .Select(x => x.Offer.CurrencyTypeId!.Value)
            .Distinct()
            .ToList();

        if (wanted.Count == 0)
            return null;

        var charged = await db
            .CurrencyTypes.Where(x =>
                wanted.Contains(x.Id) && x.ActivityPointType != null && x.Enabled
            )
            .CountAsync(ct)
            .ConfigureAwait(false);

        return charged == wanted.Count
            ? null
            : "That currency is not an activity-point currency the hotel charges.";
    }

    /// <summary>
    /// The place a page put at <paramref name="index"/> among a parent's pages takes; those from
    /// there on move down one, so the order is the one asked for whatever the old numbers were.
    /// </summary>
    private static async Task<int> PlaceAmongAsync(
        TurboDbContext db,
        int parentId,
        int index,
        CancellationToken ct
    )
    {
        var siblings = await db
            .CatalogPages.Where(x => x.ParentEntityId == parentId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Localization)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var at = Math.Clamp(index, 0, siblings.Count);

        for (var i = 0; i < siblings.Count; i++)
            siblings[i].SortOrder = i < at ? i : i + 1;

        return at;
    }

    /// <summary>A query over many ids, a thousand at a time, so no statement carries them all.</summary>
    private static async Task<List<T>> LoadInChunksAsync<T>(
        List<int> ids,
        Func<List<int>, IQueryable<T>> query,
        CancellationToken ct
    )
    {
        var rows = new List<T>();

        foreach (var chunk in ids.Chunk(1000))
            rows.AddRange(await query([.. chunk]).ToListAsync(ct).ConfigureAwait(false));

        return rows;
    }
}
