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
/// onto pages under it, by kind, by colour, or by kind and then each kind by colour.
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

    /// <summary>One way of sorting: what a furni is told apart by, a group's title, and the leftovers' title.</summary>
    private sealed record Level(
        Func<Definition, string?> Key,
        Func<string, IReadOnlyList<Definition>, string> Title,
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

        static CatalogAddFurniPage View(PlannedPage page) =>
            new(page.Title, page.PageId, page.Count, [.. page.Pages.Select(View)]);
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

        var newPages = new List<CatalogNewPage>();
        var offers = new List<CatalogNewOffer>();

        // A page is made before the pages under it, which refer to it.
        void Place(PlannedPage page, CatalogPageRef parent)
        {
            CatalogPageRef target;

            if (page.PageId is { } savedId)
                target = CatalogPageRef.Saved(savedId);
            else
            {
                target = CatalogPageRef.Made(newPages.Count);
                newPages.Add(
                    new CatalogNewPage(
                        parent,
                        new CatalogPageDraft(
                            page.Title,
                            null,
                            plan.Icon,
                            LAYOUT,
                            [],
                            [],
                            plan.Display
                        )
                    )
                );
            }

            foreach (var definitionId in page.DefinitionIds)
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
                );

            foreach (var child in page.Pages)
                Place(child, target);
        }

        foreach (var page in plan.Pages)
            Place(page, CatalogPageRef.Saved(pageId));

        var label =
            newPages.Count == 0
                ? $"added {offers.Count} furni to page {pageId}"
                : $"added {offers.Count} furni to page {pageId}, making {newPages.Count} {(newPages.Count == 1 ? "page" : "pages")} under it";
        var result = await editor
            .BuildTreeAsync(who, label, new CatalogTreeDraft(newPages, offers, [], []), ct)
            .ConfigureAwait(false);

        return result.Saved
            ? CatalogBuildOutcome<CatalogBulkResponse>.Done(
                new CatalogBulkResponse(result.OffersCreated, editor.UnpublishedChanges, [])
            )
            : CatalogBuildOutcome<CatalogBulkResponse>.Refused(result.Error!);
    }

    /// <summary>The word a furni's kind is told by, lower case; see the class.</summary>
    public static string KindOf(string name, string? line)
    {
        var words = Words(name, line);

        return words.Length > 0 ? words[0] : name.ToLowerInvariant();
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
    private static string TitleOf(IReadOnlyList<Definition> furni)
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
    private static List<Group> Sort(IReadOnlyList<Definition> furni, IReadOnlyList<Level> levels)
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

    private sealed record Definition(
        int Id,
        string Name,
        string? Line,
        string? PublicName,
        List<string>? PartColors,
        ProductType Type
    );

    private sealed record Group(string Title, List<Definition> Furni, List<Group> Groups);

    private sealed record PlannedPage(
        string Title,
        int? PageId,
        int Count,
        IReadOnlyList<int> DefinitionIds,
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
            return Plan.Refused("Furni are sorted by kind, by colour, or by kind and then colour.");

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
            .Select(x => new Definition(
                x.Id,
                x.Name,
                x.FurniLine,
                x.PublicName,
                x.PartColors,
                x.ProductType
            ))
            .ToDictionaryAsync(x => x.Id, ct)
            .ConfigureAwait(false);

        foreach (var id in ids)
        {
            if (!found.ContainsKey(id))
                return Plan.Refused($"There is no floor or wall item {id}.");
        }

        var types = found.ToDictionary(x => x.Key, x => x.Value.Type);
        var groups = Sort([.. ids.Select(x => found[x])], levels);

        if (groups.Count == 0)
            return new Plan(
                null,
                [new PlannedPage(page.Localization, pageId, ids.Count, ids, [])],
                types,
                page.Icon,
                page.Display
            );

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

        // A group goes on the page of its title under its page when there is one.
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

        return new Plan(
            null,
            [.. groups.Select(x => Planned(x, pageId))],
            types,
            page.Icon,
            page.Display
        );
    }
}
