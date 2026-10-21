using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Catalog;

/// <summary>
/// Pages of one name made one, as imports made apart, or a catalogue copied twice, leave them:
/// the others' offers go to the end of the page kept (one giving only what the kept page gives
/// already is deleted instead, when it can be), the pages under them go under it, and they are
/// deleted; the kept page is shown in each catalogue one of them was, but for a tab, which the
/// Builders Club never shows. One step to undo, through <see cref="ICatalogEditService"/>, so
/// every check an edit by hand gets applies; when one is refused, nothing stays.
/// </summary>
public sealed class AdminCatalogPageMerge(
    IDbContextFactory<TurboDbContext> database,
    ICatalogEditService editor,
    ILogger<AdminCatalogPageMerge> logger
)
{
    public async Task<CatalogBuildOutcome<CatalogPageMergeResult>> MergeAsync(
        PlayerId who,
        int keepId,
        IReadOnlyCollection<int> pageIds,
        CancellationToken ct
    )
    {
        var others = pageIds.Where(x => x != keepId).Distinct().Order().ToList();

        if (others.Count == 0)
            return Refused("Pick the pages to merge into it.");

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var pages = await db
            .CatalogPages.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct)
            .ConfigureAwait(false);

        if (!pages.TryGetValue(keepId, out var keep))
            return Refused("That page is gone.");

        if (others.Any(x => !pages.ContainsKey(x)))
            return Refused("A page to merge is gone.");

        if (keep.ParentEntityId is null || others.Any(x => pages[x].ParentEntityId is null))
            return Refused("The catalog's root can't be merged.");

        var name = keep.Name?.Trim() ?? string.Empty;

        if (name.Length == 0 || others.Any(x => pages[x].Name?.Trim() != name))
            return Refused("Only pages of one name are merged.");

        // Its pages going under the page kept, a page above it would end up under itself.
        for (
            var above = keep.ParentEntityId;
            above is { } id && pages.TryGetValue(id, out var up);
            above = up.ParentEntityId
        )
        {
            if (others.Contains(id))
                return Refused(
                    "A page can't be merged into one under it: merge into that page instead."
                );
        }

        var offers = await db
            .CatalogOffers.AsNoTracking()
            .Where(x => others.Contains(x.CatalogPageEntityId) || x.CatalogPageEntityId == keepId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.CatalogPageEntityId,
                Gives = x.Products!.OrderBy(p => p.Id)
                    .Select(p => new
                    {
                        p.ProductType,
                        p.FurnitureDefinitionEntityId,
                        p.ExtraParam,
                        p.Quantity,
                    })
                    .ToList(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        string KeyOf(int offerId) =>
            string.Join(
                ';',
                offers
                    .Single(x => x.Id == offerId)
                    .Gives.Select(p =>
                        $"{p.ProductType}:{p.FurnitureDefinitionEntityId}:{p.ExtraParam?.Trim()}:{p.Quantity}"
                    )
            );

        var given = offers
            .Where(x => x.CatalogPageEntityId == keepId)
            .Select(x => KeyOf(x.Id))
            .ToHashSet(StringComparer.Ordinal);
        var tab = pages[keep.ParentEntityId.Value].ParentEntityId is null;
        var display = tab
            ? keep.Display
            : Union([keep.Display, .. others.Select(x => pages[x].Display)]);
        var offersMoved = 0;
        var offersDropped = 0;
        var pagesMoved = 0;
        var saved = false;
        var label = $"merged the pages {name}";

        var error = await editor
            .GroupAsync(
                who,
                label,
                async () =>
                {
                    foreach (var otherId in others)
                    {
                        foreach (var offer in offers.Where(x => x.CatalogPageEntityId == otherId))
                        {
                            var key = KeyOf(offer.Id);

                            // What the page kept gives already goes, unless something keeps it
                            // (a club gift, a limited series): then it moves like the rest.
                            if (given.Contains(key))
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

                            var moved = await editor
                                .MoveOfferAsync(who, offer.Id, keepId, int.MaxValue, ct)
                                .ConfigureAwait(false);

                            if (!moved.Saved)
                                return moved.Error ?? "Not saved.";

                            saved = true;
                            offersMoved++;
                            given.Add(key);
                        }

                        foreach (
                            var child in pages
                                .Values.Where(x => x.ParentEntityId == otherId)
                                .OrderBy(x => x.SortOrder)
                                .ThenBy(x => x.Id)
                        )
                        {
                            var moved = await editor
                                .MovePageAsync(who, child.Id, keepId, int.MaxValue, ct)
                                .ConfigureAwait(false);

                            if (!moved.Saved)
                                return moved.Error ?? "Not saved.";

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

                    if (display != keep.Display)
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
                                    display
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
            "Player {PlayerId} merged {Pages} pages named {Name} into page {PageId}: {Moved} offers moved, {Dropped} given twice deleted, {PagesMoved} pages moved",
            who,
            others.Count,
            name,
            keepId,
            offersMoved,
            offersDropped,
            pagesMoved
        );

        return CatalogBuildOutcome<CatalogPageMergeResult>.Done(
            new CatalogPageMergeResult(
                offersMoved,
                offersDropped,
                pagesMoved,
                others.Count,
                display.ToName(),
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
}
