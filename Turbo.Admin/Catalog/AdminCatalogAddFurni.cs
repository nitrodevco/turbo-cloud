using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Catalog;

/// <summary>
/// Floor and wall items put on a page, an offer each at one price, as one step to undo; or sorted
/// onto pages under it, by kind, by colour, or by kind and then each kind by colour. A page's own
/// offers are sorted so too, moved onto pages under it: those that sell one floor or wall item,
/// the others (bundles, pets, badges, memberships) staying where they are.
/// <para>
/// A furni's kind is the first word of its class name, the colour after <c>*</c> left off, and a
/// first word that is its furni line's own name passed over (<c>rare_fan*3</c> is a fan, in the
/// rare line). Its colour is <see cref="FurniColours"/>'. A kind or colour of
/// <see cref="GROUP_MIN"/> furni or more has a page of its own; the others share one, Other or
/// Other colours. A sort that gives fewer than two pages of their own is passed over, so the furni
/// stay on the page above: a Christmas line's furni, all named <c>xmas14_</c>, stay together, and a
/// kind with only one colour three of it share is one page. A page of a title under its page
/// already takes those furni.
/// </para>
/// </summary>
public sealed class AdminCatalogAddFurni(
    IDbContextFactory<TurboDbContext> database,
    ICatalogEditService editor
)
{
    /// <summary>The fewest furni a kind or a colour has its own page for.</summary>
    public const int GROUP_MIN = 3;

    /// <summary>The title of the page the kinds too small for one of their own share.</summary>
    public const string OTHER_TITLE = "Other";

    /// <summary>The title of the page the colours too small for one of their own share.</summary>
    public const string OTHER_COLOURS_TITLE = "Other colours";

    /// <summary>Sorted onto a page for each kind.</summary>
    public const string SORT_KIND = "kind";

    /// <summary>Sorted onto a page for each colour.</summary>
    public const string SORT_COLOUR = "colour";

    /// <summary>Sorted onto a page for each kind, and each kind onto a page for each colour.</summary>
    public const string SORT_KIND_COLOUR = "kindColour";

    private const string LAYOUT = "default_3x3";

    private const string UNKNOWN_SORT =
        "Furni are sorted by kind, by colour, or by kind and then colour.";

    /// <summary>One way of sorting: what a furni is told apart by, a group's title, and the leftovers' title.</summary>
    private sealed record Level(
        Func<Sortable, string?> Key,
        Func<string, IReadOnlyList<Sortable>, string> Title,
        string RestTitle
    );

    private static readonly Level ByKind = new(
        x => KindOf(x.Name, x.Line),
        (_, furni) => TitleOf(furni),
        OTHER_TITLE
    );

    private static readonly Level ByColour = new(
        x => FurniColours.Of(x.PublicName, x.PartColors),
        (colour, _) => FurniColours.Title(colour),
        OTHER_COLOURS_TITLE
    );

    /// <summary>The pages the furni would go on and how many each; nothing is changed.</summary>
    public async Task<CatalogBuildOutcome<CatalogAddFurniPlan>> PlanAsync(
        int pageId,
        CatalogAddFurniRequest request,
        CancellationToken ct
    )
    {
        var plan = await PlanPagesAsync(pageId, request, ct).ConfigureAwait(false);

        return plan.Error is { } error
            ? CatalogBuildOutcome<CatalogAddFurniPlan>.Refused(error)
            : CatalogBuildOutcome<CatalogAddFurniPlan>.Done(
                new CatalogAddFurniPlan([.. plan.Pages.Select(View)])
            );
    }

    /// <summary>The furni added, the pages they need made with them: one step to undo.</summary>
    public async Task<CatalogBuildOutcome<CatalogBulkResponse>> AddAsync(
        PlayerId who,
        int pageId,
        CatalogAddFurniRequest request,
        CancellationToken ct
    )
    {
        var plan = await PlanPagesAsync(pageId, request, ct).ConfigureAwait(false);

        if (plan.Error is { } error)
            return CatalogBuildOutcome<CatalogBulkResponse>.Refused(error);

        var offers = new List<CatalogNewOffer>();
        var newPages = PagesOf(
            plan.Pages,
            pageId,
            plan.Icon,
            plan.Display,
            (target, definitionId) =>
                offers.Add(
                    new CatalogNewOffer(
                        target,
                        new CatalogOfferDraft(
                            pageId,
                            string.Empty,
                            request.CostCredits,
                            request.CostCurrency,
                            request.CurrencyTypeId,
                            request.CanGift,
                            CanBundle: true,
                            request.ClubLevel,
                            request.Visible,
                            Product: null,
                            Products:
                            [
                                new CatalogProductDraft(
                                    plan.Types[definitionId],
                                    definitionId,
                                    null,
                                    1
                                ),
                            ]
                        )
                    )
                )
        );

        var label =
            newPages.Count == 0
                ? $"added {offers.Count} furni to page {pageId}"
                : $"added {offers.Count} furni to page {pageId}, making {Pages(newPages.Count)} under it";
        var result = await editor
            .BuildTreeAsync(who, label, new CatalogTreeDraft(newPages, offers, [], []), ct)
            .ConfigureAwait(false);

        return result.Saved
            ? CatalogBuildOutcome<CatalogBulkResponse>.Done(
                new CatalogBulkResponse(result.OffersCreated, editor.UnpublishedChanges, [])
            )
            : CatalogBuildOutcome<CatalogBulkResponse>.Refused(result.Error!);
    }

    /// <summary>
    /// The pages under a page its offers would be moved onto, sorted so, and how many stay on it;
    /// no pages when the sort parts nothing. Nothing is changed.
    /// </summary>
    public async Task<CatalogBuildOutcome<CatalogResortPlan>> PlanResortAsync(
        int pageId,
        string? sortBy,
        CancellationToken ct
    )
    {
        var plan = await PlanResortPagesAsync(pageId, sortBy, ct).ConfigureAwait(false);

        return plan.Error is { } error
            ? CatalogBuildOutcome<CatalogResortPlan>.Refused(error)
            : CatalogBuildOutcome<CatalogResortPlan>.Done(
                new CatalogResortPlan([.. plan.Pages.Select(View)], plan.Staying)
            );
    }

    /// <summary>A page's offers moved onto pages under it, sorted so, the pages made with them: one step to undo.</summary>
    public async Task<CatalogBuildOutcome<CatalogBulkResponse>> ResortAsync(
        PlayerId who,
        int pageId,
        string? sortBy,
        CancellationToken ct
    )
    {
        var plan = await PlanResortPagesAsync(pageId, sortBy, ct).ConfigureAwait(false);

        if (plan.Error is { } error)
            return CatalogBuildOutcome<CatalogBulkResponse>.Refused(error);

        if (plan.Pages.Count == 0)
            return CatalogBuildOutcome<CatalogBulkResponse>.Refused(
                "Nothing to sort it into: no two kinds or colours have three offers each."
            );

        var moves = new List<CatalogOfferPlacement>();
        var newPages = PagesOf(
            plan.Pages,
            pageId,
            plan.Icon,
            plan.Display,
            (target, offerId) => moves.Add(new CatalogOfferPlacement(offerId, target))
        );
        var label =
            $"sorted the page {plan.Title} by {sortBy}, making {Pages(newPages.Count)} under it";
        var result = await editor
            .BuildTreeAsync(who, label, new CatalogTreeDraft(newPages, [], moves, []), ct)
            .ConfigureAwait(false);

        return result.Saved
            ? CatalogBuildOutcome<CatalogBulkResponse>.Done(
                new CatalogBulkResponse(result.OffersMoved, editor.UnpublishedChanges, [])
            )
            : CatalogBuildOutcome<CatalogBulkResponse>.Refused(result.Error!);
    }

    /// <summary>The word a furni's kind is told by, lower case; see the class.</summary>
    public static string KindOf(string name, string? line)
    {
        var words = Words(name, line);

        return words.Length > 0 ? words[0] : name.ToLowerInvariant();
    }

    private static string Pages(int count) => count == 1 ? "1 page" : $"{count} pages";

    private static CatalogAddFurniPage View(PlannedPage page) =>
        new(page.Title, page.PageId, page.Count, [.. page.Pages.Select(View)]);

    /// <summary>
    /// The pages of a plan to make under the page, each made before the pages under it, which
    /// refer to it; <paramref name="place"/> is told where each thing planned goes.
    /// </summary>
    private static List<CatalogNewPage> PagesOf(
        IReadOnlyList<PlannedPage> pages,
        int pageId,
        int icon,
        CatalogPageDisplay display,
        Action<CatalogPageRef, int> place
    )
    {
        var made = new List<CatalogNewPage>();

        void Place(PlannedPage page, CatalogPageRef parent)
        {
            CatalogPageRef target;

            if (page.PageId is { } savedId)
                target = CatalogPageRef.Saved(savedId);
            else
            {
                target = CatalogPageRef.Made(made.Count);
                made.Add(
                    new CatalogNewPage(
                        parent,
                        new CatalogPageDraft(page.Title, null, icon, LAYOUT, [], [], display)
                    )
                );
            }

            foreach (var id in page.Ids)
                place(target, id);

            foreach (var child in page.Pages)
                Place(child, target);
        }

        foreach (var page in pages)
            Place(page, CatalogPageRef.Saved(pageId));

        return made;
    }

    /// <summary>A class name's words but for its colour, and but for a first word that is its line's name.</summary>
    private static string[] Words(string name, string? line)
    {
        var words = name.Split('*', 2)[0]
            .ToLowerInvariant()
            .Split('_', StringSplitOptions.RemoveEmptyEntries);

        return words.Length > 1 && string.Equals(words[0], line, StringComparison.OrdinalIgnoreCase)
            ? words[1..]
            : words;
    }

    /// <summary>A kind's page title: the words every one of its names starts with, as a title.</summary>
    private static string TitleOf(IReadOnlyList<Sortable> furni)
    {
        var shared = Words(furni[0].Name, furni[0].Line);

        foreach (var x in furni.Skip(1))
        {
            var words = Words(x.Name, x.Line);
            var same = 0;

            while (same < shared.Length && same < words.Length && shared[same] == words[same])
                same++;

            shared = shared[..same];
        }

        var title = string.Join(' ', shared);

        return title.Length == 0
            ? OTHER_TITLE
            : char.ToUpper(title[0], CultureInfo.InvariantCulture) + title[1..];
    }

    /// <summary>
    /// The groups the furni sort into at the first level that parts them, each sorted by the
    /// levels after; none when no level does, the furni staying on the page above.
    /// </summary>
    private static List<Group> Sort(IReadOnlyList<Sortable> furni, IReadOnlyList<Level> levels)
    {
        for (var at = 0; at < levels.Count; at++)
        {
            var level = levels[at];
            var groups = furni.GroupBy(level.Key).ToList();
            var own = groups
                .Where(g => g.Key is not null && g.Count() >= GROUP_MIN)
                .Select(g => (Key: g.Key!, Furni: g.ToList()))
                .ToList();
            var ownKeys = own.Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
            var rest = furni
                .Where(x => level.Key(x) is not { } key || !ownKeys.Contains(key))
                .ToList();

            // One group of its own beside the rest sorts nothing worth a page each.
            if (own.Count < 2)
                continue;

            var after = levels.Skip(at + 1).ToList();
            var sorted = own.Select(g => new Group(
                    level.Title(g.Key, g.Furni),
                    g.Furni,
                    Sort(g.Furni, after)
                ))
                .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (rest.Count > 0)
                sorted.Add(new Group(level.RestTitle, rest, Sort(rest, after)));

            return sorted;
        }

        return [];
    }

    private static IReadOnlyList<Level>? LevelsOf(string? sortBy) =>
        sortBy switch
        {
            null or "" => [],
            SORT_KIND => [ByKind],
            SORT_COLOUR => [ByColour],
            SORT_KIND_COLOUR => [ByKind, ByColour],
            _ => null,
        };

    /// <summary>
    /// What is sorted: a definition to add, or an offer to move, by its <see cref="Id"/>; and the
    /// furni it is, which it is sorted by.
    /// </summary>
    private sealed record Sortable(
        int Id,
        string Name,
        string? Line,
        string? PublicName,
        List<string>? PartColors
    );

    private sealed record Group(string Title, List<Sortable> Furni, List<Group> Groups);

    /// <summary>A page planned: one there already, or one to make; what goes on it, by id, and the pages under it.</summary>
    private sealed record PlannedPage(
        string Title,
        int? PageId,
        int Count,
        IReadOnlyList<int> Ids,
        IReadOnlyList<PlannedPage> Pages
    );

    private sealed record Plan(
        string? Error,
        IReadOnlyList<PlannedPage> Pages,
        IReadOnlyDictionary<int, ProductType> Types,
        int Icon,
        CatalogPageDisplay Display
    )
    {
        public static Plan Refused(string error) =>
            new(error, [], new Dictionary<int, ProductType>(), 0, default);
    }

    private sealed record ResortPlan(
        string? Error,
        string Title,
        IReadOnlyList<PlannedPage> Pages,
        int Staying,
        int Icon,
        CatalogPageDisplay Display
    )
    {
        public static ResortPlan Refused(string error) =>
            new(error, string.Empty, [], 0, 0, default);
    }

    private async Task<Plan> PlanPagesAsync(
        int pageId,
        CatalogAddFurniRequest request,
        CancellationToken ct
    )
    {
        var ids = (request.DefinitionIds ?? []).Distinct().ToList();

        if (ids.Count == 0)
            return Plan.Refused("Pick the furni to add.");

        if (LevelsOf(request.SortBy) is not { } levels)
            return Plan.Refused(UNKNOWN_SORT);

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var page = await db
            .CatalogPages.AsNoTracking()
            .Where(x => x.Id == pageId)
            .Select(x => new
            {
                x.Localization,
                x.Icon,
                x.Display,
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (page is null)
            return Plan.Refused("That page is gone.");

        var found = await db
            .FurnitureDefinitions.AsNoTracking()
            .Where(x =>
                ids.Contains(x.Id)
                && (x.ProductType == ProductType.Floor || x.ProductType == ProductType.Wall)
            )
            .Select(x => new
            {
                Furni = new Sortable(x.Id, x.Name, x.FurniLine, x.PublicName, x.PartColors),
                x.ProductType,
            })
            .ToDictionaryAsync(x => x.Furni.Id, ct)
            .ConfigureAwait(false);

        foreach (var id in ids)
        {
            if (!found.ContainsKey(id))
                return Plan.Refused($"There is no floor or wall item {id}.");
        }

        var types = found.ToDictionary(x => x.Key, x => x.Value.ProductType);
        var groups = Sort([.. ids.Select(x => found[x].Furni)], levels);

        if (groups.Count == 0)
            return new Plan(
                null,
                [new PlannedPage(page.Localization, pageId, ids.Count, ids, [])],
                types,
                page.Icon,
                page.Display
            );

        return new Plan(
            null,
            await PlannedAsync(db, pageId, groups, ct).ConfigureAwait(false),
            types,
            page.Icon,
            page.Display
        );
    }

    private async Task<ResortPlan> PlanResortPagesAsync(
        int pageId,
        string? sortBy,
        CancellationToken ct
    )
    {
        if (string.IsNullOrEmpty(sortBy) || LevelsOf(sortBy) is not { } levels)
            return ResortPlan.Refused(UNKNOWN_SORT);

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var page = await db
            .CatalogPages.AsNoTracking()
            .Where(x => x.Id == pageId)
            .Select(x => new
            {
                x.ParentEntityId,
                x.Localization,
                x.Icon,
                x.Display,
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (page is null)
            return ResortPlan.Refused("That page is gone.");

        if (page.ParentEntityId is null)
            return ResortPlan.Refused("The catalog's root has no offers to sort.");

        var offers = await db
            .CatalogOffers.AsNoTracking()
            .Where(x => x.CatalogPageEntityId == pageId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Products = x.Products!.Select(p => new
                    {
                        p.ProductType,
                        p.FurnitureDefinitionEntityId,
                        p.FurnitureDefinition,
                    })
                    .ToList(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // An offer of one floor or wall item is sorted by it; any other stays on the page.
        var furni = offers
            .Where(x =>
                x.Products.Count == 1
                && x.Products[0].ProductType is ProductType.Floor or ProductType.Wall
                && x.Products[0].FurnitureDefinition is not null
            )
            .Select(x =>
            {
                var definition = x.Products[0].FurnitureDefinition!;

                return new Sortable(
                    x.Id,
                    definition.Name,
                    definition.FurniLine,
                    definition.PublicName,
                    definition.PartColors
                );
            })
            .ToList();
        var groups = Sort(furni, levels);

        if (groups.Count == 0)
            return new ResortPlan(
                null,
                page.Localization,
                [],
                offers.Count,
                page.Icon,
                page.Display
            );

        return new ResortPlan(
            null,
            page.Localization,
            await PlannedAsync(db, pageId, groups, ct).ConfigureAwait(false),
            offers.Count - furni.Count,
            page.Icon,
            page.Display
        );
    }

    /// <summary>The groups as pages under the page; a group goes on the page of its title under its page when there is one.</summary>
    private static async Task<IReadOnlyList<PlannedPage>> PlannedAsync(
        TurboDbContext db,
        int pageId,
        List<Group> groups,
        CancellationToken ct
    )
    {
        var children = (
            await db
                .CatalogPages.AsNoTracking()
                .Where(x => x.ParentEntityId != null)
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    ParentId = x.ParentEntityId!.Value,
                    x.Localization,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        ).ToLookup(x => x.ParentId);

        PlannedPage Planned(Group group, int? parentId)
        {
            var id = parentId is { } parent
                ? children[parent]
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.Localization.Trim(),
                            group.Title,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    ?.Id
                : null;

            return new PlannedPage(
                group.Title,
                id,
                group.Furni.Count,
                group.Groups.Count == 0 ? [.. group.Furni.Select(x => x.Id)] : [],
                [.. group.Groups.Select(x => Planned(x, id))]
            );
        }

        return [.. groups.Select(x => Planned(x, pageId))];
    }
}
