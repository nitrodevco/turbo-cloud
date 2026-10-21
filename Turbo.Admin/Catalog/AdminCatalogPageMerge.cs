using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Catalog;

/// <summary>
/// Pages made one, as imports made apart, or a catalogue copied twice, leave them, or as an editor
/// picks: the others' offers go to the end of the page kept, the pages under them go under it, and
/// they are deleted; the kept page is shown in each catalogue one of them was, but for a tab, which
/// the Builders Club never shows. Where two of the pages sell the same thing (the same products),
/// the offer on the page given priority stays - the kept page by default - and the others are
/// deleted when they can be; one that stays from another page takes the deleted one's place on the
/// kept page. One step to undo, through <see cref="ICatalogEditService"/>, so every check an edit
/// by hand gets applies; when one is refused, nothing stays.
/// </summary>
public sealed class AdminCatalogPageMerge(
    IDbContextFactory<TurboDbContext> database,
    ICatalogEditService editor,
    ILogger<AdminCatalogPageMerge> logger
)
{
    /// <summary>What merging would do, with each thing two of the pages sell and whose offer stays; nothing is changed.</summary>
    public async Task<CatalogBuildOutcome<CatalogPageMergePlan>> PreviewAsync(
        int keepId,
        IReadOnlyCollection<int> pageIds,
        int? priorityId,
        CancellationToken ct
    )
    {
        var plan = await PlanAsync(keepId, pageIds, priorityId, ct).ConfigureAwait(false);

        if (plan.Error is { } error)
            return CatalogBuildOutcome<CatalogPageMergePlan>.Refused(error);

        var losers = plan.Offers.Where(plan.Loses).ToList();

        return CatalogBuildOutcome<CatalogPageMergePlan>.Done(
            new CatalogPageMergePlan(
                plan.Offers.Count(x => x.PageId != keepId && !plan.Loses(x)),
                losers.Count,
                plan.ChildrenOf.Values.Sum(x => x.Count),
                plan.Others.Count,
                plan.Display.ToName(),
                [
                    .. losers
                        .GroupBy(x => x.Key, StringComparer.Ordinal)
                        .Select(g => new CatalogPageMergeDuplicate(
                            plan.Winners[g.Key].Name,
                            View(plan.Winners[g.Key]),
                            [.. g.Select(View)]
                        )),
                ]
            )
        );

        static CatalogPageMergeOffer View(MergeOffer x) =>
            new(x.Id, x.PageId, x.CostCredits, x.CostCurrency, x.CurrencyTypeId);
    }

    public async Task<CatalogBuildOutcome<CatalogPageMergeResult>> MergeAsync(
        PlayerId who,
        int keepId,
        IReadOnlyCollection<int> pageIds,
        int? priorityId,
        CancellationToken ct
    )
    {
        var plan = await PlanAsync(keepId, pageIds, priorityId, ct).ConfigureAwait(false);

        if (plan.Error is { } planError)
            return Refused(planError);

        var keep = plan.Keep!;
        var offersMoved = 0;
        var offersDropped = 0;
        var pagesMoved = 0;
        var saved = false;
        var moved = new HashSet<int>();

        // The kept page's offers in its order, as the moves and deletes leave it.
        var kept = plan.Offers.Where(x => x.PageId == keepId).Select(x => x.Id).ToList();

        var error = await editor
            .GroupAsync(
                who,
                plan.Label,
                async () =>
                {
                    // What the kept page sells that another page's offer wins: that offer takes
                    // its place, and it goes, unless something keeps it (a club gift, a limited
                    // series).
                    foreach (
                        var offer in plan.Offers.Where(x => x.PageId == keepId && plan.Loses(x))
                    )
                    {
                        var winner = plan.Winners[offer.Key];

                        if (moved.Add(winner.Id))
                        {
                            var index = kept.IndexOf(offer.Id);
                            var placed = await editor
                                .MoveOfferAsync(who, winner.Id, keepId, index, ct)
                                .ConfigureAwait(false);

                            if (!placed.Saved)
                                return placed.Error ?? "Not saved.";

                            saved = true;
                            offersMoved++;
                            kept.Insert(index, winner.Id);
                        }

                        var deleted = await editor
                            .DeleteOfferAsync(who, offer.Id, ct)
                            .ConfigureAwait(false);

                        if (deleted.Saved)
                        {
                            saved = true;
                            offersDropped++;
                            kept.Remove(offer.Id);
                        }
                    }

                    foreach (var otherId in plan.Others)
                    {
                        foreach (var offer in plan.Offers.Where(x => x.PageId == otherId))
                        {
                            if (moved.Contains(offer.Id))
                                continue;

                            if (plan.Loses(offer))
                            {
                                var deleted = await editor
                                    .DeleteOfferAsync(who, offer.Id, ct)
                                    .ConfigureAwait(false);

                                if (deleted.Saved)
                                {
                                    saved = true;
                                    offersDropped++;

                                    continue;
                                }
                            }

                            var placed = await editor
                                .MoveOfferAsync(who, offer.Id, keepId, int.MaxValue, ct)
                                .ConfigureAwait(false);

                            if (!placed.Saved)
                                return placed.Error ?? "Not saved.";

                            saved = true;
                            offersMoved++;
                            moved.Add(offer.Id);
                        }

                        foreach (var child in plan.ChildrenOf[otherId])
                        {
                            var placed = await editor
                                .MovePageAsync(who, child, keepId, int.MaxValue, ct)
                                .ConfigureAwait(false);

                            if (!placed.Saved)
                                return placed.Error ?? "Not saved.";

                            saved = true;
                            pagesMoved++;
                        }

                        var gone = await editor
                            .DeletePageAsync(who, otherId, ct)
                            .ConfigureAwait(false);

                        if (!gone.Saved)
                            return gone.Error ?? "Not saved.";

                        saved = true;
                    }

                    if (plan.Display != keep.Display)
                    {
                        var shown = await editor
                            .UpdatePageAsync(
                                who,
                                keepId,
                                new CatalogPageDraft(
                                    keep.Localization,
                                    keep.Name,
                                    keep.Icon,
                                    keep.Layout,
                                    [.. keep.ImageData ?? []],
                                    [.. keep.TextData ?? []],
                                    plan.Display
                                ),
                                ct
                            )
                            .ConfigureAwait(false);

                        if (!shown.Saved)
                            return shown.Error ?? "Not saved.";
                    }

                    return (string?)null;
                }
            )
            .ConfigureAwait(false);

        if (error is not null)
        {
            if (saved)
                await editor.UndoAsync(who, ct).ConfigureAwait(false);

            return Refused(error);
        }

        logger.LogInformation(
            "Player {PlayerId} {Label} (page {PageId}, priority to page {PriorityId}): {Moved} offers moved, {Dropped} sold twice deleted, {PagesMoved} pages moved",
            who,
            plan.Label,
            keepId,
            plan.PriorityId,
            offersMoved,
            offersDropped,
            pagesMoved
        );

        return CatalogBuildOutcome<CatalogPageMergeResult>.Done(
            new CatalogPageMergeResult(
                offersMoved,
                offersDropped,
                pagesMoved,
                plan.Others.Count,
                plan.Display.ToName(),
                editor.UnpublishedChanges
            )
        );
    }

    /// <summary>Shown in each catalogue one of the displays shows a page in; hidden only when every one is.</summary>
    internal static CatalogPageDisplay Union(IReadOnlyCollection<CatalogPageDisplay> displays)
    {
        var normal = displays.Any(x => x is CatalogPageDisplay.Regular or CatalogPageDisplay.Both);
        var builders = displays.Any(x => x.IsInBuildersClub());

        return normal && builders ? CatalogPageDisplay.Both
            : normal ? CatalogPageDisplay.Regular
            : builders ? CatalogPageDisplay.BuildersClubOnly
            : CatalogPageDisplay.Invisible;
    }

    private static CatalogBuildOutcome<CatalogPageMergeResult> Refused(string why) =>
        CatalogBuildOutcome<CatalogPageMergeResult>.Refused(why);

    /// <summary>An offer on one of the pages: what it gives, as a key, and what it costs.</summary>
    private sealed record MergeOffer(
        int Id,
        int PageId,
        string Key,
        string Name,
        int CostCredits,
        int CostCurrency,
        int? CurrencyTypeId
    );

    /// <summary>
    /// A merge worked out: the pages, their offers in order (the kept page's first, then the
    /// others'), and for each thing sold the offer that stays.
    /// </summary>
    private sealed record Plan(
        string? Error,
        CatalogPageEntity? Keep,
        IReadOnlyList<int> Others,
        int PriorityId,
        IReadOnlyList<MergeOffer> Offers,
        IReadOnlyDictionary<string, MergeOffer> Winners,
        IReadOnlyDictionary<int, List<int>> ChildrenOf,
        CatalogPageDisplay Display,
        string Label
    )
    {
        /// <summary>
        /// Whether an offer goes, another page's offer selling the same thing; two offers of one
        /// page are left as they are.
        /// </summary>
        public bool Loses(MergeOffer offer) => Winners[offer.Key].PageId != offer.PageId;

        public static Plan Refused(string error) =>
            new(
                error,
                null,
                [],
                0,
                [],
                new Dictionary<string, MergeOffer>(),
                new Dictionary<int, List<int>>(),
                default,
                string.Empty
            );
    }

    private async Task<Plan> PlanAsync(
        int keepId,
        IReadOnlyCollection<int> pageIds,
        int? priorityId,
        CancellationToken ct
    )
    {
        var others = pageIds.Where(x => x != keepId).Distinct().Order().ToList();

        if (others.Count == 0)
            return Plan.Refused("Pick the pages to merge into it.");

        var priority = priorityId ?? keepId;

        if (priority != keepId && !others.Contains(priority))
            return Plan.Refused("The page whose offers stay has to be one of those merged.");

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var pages = await db
            .CatalogPages.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct)
            .ConfigureAwait(false);

        if (!pages.TryGetValue(keepId, out var keep))
            return Plan.Refused("That page is gone.");

        if (others.Any(x => !pages.ContainsKey(x)))
            return Plan.Refused("A page to merge is gone.");

        if (keep.ParentEntityId is null || others.Any(x => pages[x].ParentEntityId is null))
            return Plan.Refused("The catalog's root can't be merged.");

        // Its pages going under the page kept, a page above it would end up under itself.
        for (
            var above = keep.ParentEntityId;
            above is { } id && pages.TryGetValue(id, out var up);
            above = up.ParentEntityId
        )
        {
            if (others.Contains(id))
                return Plan.Refused(
                    "A page can't be merged into one under it: merge into that page instead."
                );
        }

        var rows = await db
            .CatalogOffers.AsNoTracking()
            .Where(x => others.Contains(x.CatalogPageEntityId) || x.CatalogPageEntityId == keepId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.CatalogPageEntityId,
                x.LocalizationId,
                x.CostCredits,
                x.CostCurrency,
                x.CurrencyTypeId,
                Gives = x.Products!.OrderBy(p => p.Id)
                    .Select(p => new
                    {
                        p.ProductType,
                        p.FurnitureDefinitionEntityId,
                        p.ExtraParam,
                        p.Quantity,
                        Name = p.FurnitureDefinition != null ? p.FurnitureDefinition.Name : null,
                    })
                    .ToList(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var offers = rows.Select(x => new MergeOffer(
                x.Id,
                x.CatalogPageEntityId,
                string.Join(
                    ';',
                    x.Gives.Select(p =>
                        $"{p.ProductType}:{p.FurnitureDefinitionEntityId}:{p.ExtraParam?.Trim()}:{p.Quantity}"
                    )
                ),
                x.Gives.FirstOrDefault()?.Name ?? x.LocalizationId,
                x.CostCredits,
                x.CostCurrency,
                x.CurrencyTypeId
            ))
            .OrderBy(x => x.PageId == keepId ? -1 : others.IndexOf(x.PageId))
            .ToList();

        // For each thing sold, the first offer of the page given priority, then of the kept page,
        // then of the others in order.
        var winners = new Dictionary<string, MergeOffer>(StringComparer.Ordinal);

        foreach (var offer in offers.OrderBy(x => x.PageId == priority ? 0 : 1))
            winners.TryAdd(offer.Key, offer);

        var childrenOf = others.ToDictionary(
            x => x,
            x =>
                pages
                    .Values.Where(p => p.ParentEntityId == x && !others.Contains(p.Id))
                    .OrderBy(p => p.SortOrder)
                    .ThenBy(p => p.Id)
                    .Select(p => p.Id)
                    .ToList()
        );
        var tab = pages[keep.ParentEntityId.Value].ParentEntityId is null;
        var display = tab
            ? keep.Display
            : Union([keep.Display, .. others.Select(x => pages[x].Display)]);
        var name = keep.Name?.Trim() ?? string.Empty;
        var label =
            name.Length > 0 && others.All(x => pages[x].Name?.Trim() == name)
                ? $"merged the pages {name}"
            : others.Count == 1
                ? $"merged the page {pages[others[0]].Localization} into {keep.Localization}"
            : $"merged {others.Count} pages into {keep.Localization}";

        return new Plan(null, keep, others, priority, offers, winners, childrenOf, display, label);
    }
}
