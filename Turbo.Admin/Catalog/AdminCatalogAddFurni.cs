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
/// onto a page under it for each kind of furni. A furni's kind is the first word of its class
/// name, the colour after <c>*</c> left off, and a first word that is its furni line's own name
/// passed over (<c>rare_fan*3</c> is a fan, in the rare line); kinds of fewer than
/// <see cref="KIND_MIN"/> furni share a page. When they come to one page, they go on the page
/// itself: a Christmas line's furni, all named <c>xmas14_</c>, stay together. A page of a kind's
/// title under it already takes that kind's furni.
/// </summary>
public sealed class AdminCatalogAddFurni(
    IDbContextFactory<TurboDbContext> database,
    ICatalogEditService editor
)
{
    /// <summary>The fewest furni a kind has its own page for.</summary>
    public const int KIND_MIN = 3;

    /// <summary>The title of the page the kinds too small for one of their own share.</summary>
    public const string OTHER_TITLE = "Other";

    private const string LAYOUT = "default_3x3";

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
                new CatalogAddFurniPlan([
                    .. plan.Pages.Select(x => new CatalogAddFurniPage(
                        x.Title,
                        x.PageId,
                        x.DefinitionIds.Count
                    )),
                ])
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

        var newPages = new List<CatalogNewPage>();
        var offers = new List<CatalogNewOffer>();

        foreach (var page in plan.Pages)
        {
            CatalogPageRef target;

            if (page.PageId is { } savedId)
                target = CatalogPageRef.Saved(savedId);
            else
            {
                target = CatalogPageRef.Made(newPages.Count);
                newPages.Add(
                    new CatalogNewPage(
                        CatalogPageRef.Saved(pageId),
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
        }

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

    private sealed record Definition(int Id, string Name, string? Line, ProductType Type);

    private sealed record PlannedPage(string Title, int? PageId, IReadOnlyList<int> DefinitionIds);

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
            .Select(x => new Definition(x.Id, x.Name, x.FurniLine, x.ProductType))
            .ToDictionaryAsync(x => x.Id, ct)
            .ConfigureAwait(false);

        foreach (var id in ids)
        {
            if (!found.ContainsKey(id))
                return Plan.Refused($"There is no floor or wall item {id}.");
        }

        var types = found.ToDictionary(x => x.Key, x => x.Value.Type);
        var furni = ids.Select(x => found[x]).ToList();
        var kinds = request.SubPages
            ? furni.GroupBy(x => KindOf(x.Name, x.Line), StringComparer.Ordinal).ToList()
            : [];
        var own = kinds.Where(g => g.Count() >= KIND_MIN).ToList();
        var others = kinds.Where(g => g.Count() < KIND_MIN).SelectMany(g => g).ToList();

        if (own.Count + (others.Count > 0 ? 1 : 0) <= 1)
            return new Plan(
                null,
                [new PlannedPage(page.Localization, pageId, ids)],
                types,
                page.Icon,
                page.Display
            );

        var children = await db
            .CatalogPages.AsNoTracking()
            .Where(x => x.ParentEntityId == pageId)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Localization })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        int? Existing(string title) =>
            children
                .FirstOrDefault(x =>
                    string.Equals(x.Localization.Trim(), title, StringComparison.OrdinalIgnoreCase)
                )
                ?.Id;

        var pages = own.Select(g => g.ToList())
            .Select(g => (Title: TitleOf(g), Furni: g))
            .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (others.Count > 0)
            pages.Add((OTHER_TITLE, others));

        return new Plan(
            null,
            [
                .. pages.Select(x => new PlannedPage(
                    x.Title,
                    Existing(x.Title),
                    [.. x.Furni.Select(f => f.Id)]
                )),
            ],
            types,
            page.Icon,
            page.Display
        );
    }
}
