using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Pets;

namespace Turbo.Admin.Catalog;

/// <summary>
/// The catalog editor's audits, read from the rows as saved: the floor and wall items the catalog
/// does not sell (anywhere, or anywhere players can see), and the items it sells in more than one
/// offer. Read-only.
/// </summary>
public sealed class AdminCatalogAudit(IDbContextFactory<TurboDbContext> database)
{
    /// <summary>The most furni a page of the unoffered list holds.</summary>
    public const int PAGE_SIZE_MAX = 500;

    /// <summary>Furni in no offer at all.</summary>
    public const string SCOPE_MISSING = "missing";

    /// <summary>Furni whose every offer is hidden, or on a page players can't reach.</summary>
    public const string SCOPE_HIDDEN = "hidden";

    /// <summary>
    /// The furni the catalog does not sell, by name, narrowed to a text in the class or public
    /// name, a furni line and a furnidata category. Patterns, posters and songs (one item, sold
    /// by what its product names) and pets are left out: their builders sell them.
    /// </summary>
    public async Task<CatalogUnofferedResponse> GetUnofferedAsync(
        string? scope,
        string? text,
        string? line,
        string? category,
        int page,
        int size,
        CancellationToken ct
    )
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var furni = await FurniAsync(db, ct).ConfigureAwait(false);
        var sold = await SoldAsync(db, ct).ConfigureAwait(false);
        var hiddenOnly = string.Equals(scope, SCOPE_HIDDEN, StringComparison.OrdinalIgnoreCase);
        var unoffered = furni
            .Where(x => sold.TryGetValue(x.Id, out var shown) ? hiddenOnly && !shown : !hiddenOnly)
            .ToList();
        var needle = text?.Trim() ?? string.Empty;
        var narrowed = unoffered
            .Where(x =>
                (
                    needle.Length == 0
                    || x.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || (x.PublicName?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
                )
                && (
                    string.IsNullOrEmpty(line)
                    || string.Equals(x.Line ?? string.Empty, line, StringComparison.Ordinal)
                )
                && (
                    string.IsNullOrEmpty(category)
                    || string.Equals(x.Category ?? string.Empty, category, StringComparison.Ordinal)
                )
            )
            .OrderBy(x => x.Line ?? "￿", StringComparer.Ordinal)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
        var take = Math.Clamp(size, 1, PAGE_SIZE_MAX);

        return new CatalogUnofferedResponse(
            narrowed.Count,
            [.. narrowed.Skip(Math.Max(0, page) * take).Take(take)],
            Facets(unoffered.Select(x => x.Line)),
            Facets(unoffered.Select(x => x.Category))
        );
    }

    /// <summary>
    /// Every furni sold alone by more than one offer, as one furni with the pattern or number it
    /// carries: where each offer is, what it costs and whether players see it. Club gifts and
    /// bundles are left out, being meant to repeat what is sold elsewhere.
    /// </summary>
    public async Task<CatalogDuplicatesResponse> GetDuplicatesAsync(CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var furni = (await FurniAsync(db, ct).ConfigureAwait(false)).ToDictionary(x => x.Id);
        var pages = await PagesAsync(db, ct).ConfigureAwait(false);
        var offers = await db
            .CatalogOffers.AsNoTracking()
            .Where(x => x.ClubGiftDaysRequired == null && x.Products!.Count == 1)
            .Select(x => new
            {
                x.Id,
                x.CatalogPageEntityId,
                x.CostCredits,
                x.CostCurrency,
                x.CurrencyTypeId,
                x.Visible,
                Product = x.Products!.Select(p => new
                    {
                        p.FurnitureDefinitionEntityId,
                        p.ExtraParam,
                        p.ProductType,
                    })
                    .First(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var duplicates = offers
            .Where(x =>
                x.Product.FurnitureDefinitionEntityId != null
                && x.Product.ProductType is ProductType.Floor or ProductType.Wall
            )
            .GroupBy(x =>
                (
                    Id: x.Product.FurnitureDefinitionEntityId!.Value,
                    Param: x.Product.ExtraParam?.Trim() ?? string.Empty
                )
            )
            .Where(g => g.Count() > 1 && furni.ContainsKey(g.Key.Id))
            .Select(g => new CatalogDuplicate(
                furni[g.Key.Id],
                g.Key.Param.Length > 0 ? g.Key.Param : null,
                [
                    .. g.OrderBy(x => x.Id)
                        .Select(x => new CatalogDuplicateOffer(
                            x.Id,
                            x.CatalogPageEntityId,
                            pages.GetValueOrDefault(x.CatalogPageEntityId)?.Title ?? "?",
                            PathOf(pages, x.CatalogPageEntityId),
                            x.CostCredits,
                            x.CostCurrency,
                            x.CurrencyTypeId,
                            x.Visible,
                            x.Visible && IsShown(pages, x.CatalogPageEntityId)
                        )),
                ]
            ))
            .OrderByDescending(x => x.Offers.Length)
            .ThenBy(x => x.Furni.Name, StringComparer.Ordinal)
            .ToArray();

        return new CatalogDuplicatesResponse(duplicates.Length, duplicates);
    }

    /// <summary>The floor and wall items a plain offer can sell: not the patterned ones, nor pets.</summary>
    internal static async Task<List<CatalogAuditFurni>> FurniAsync(
        TurboDbContext db,
        CancellationToken ct
    )
    {
        var rows = await db
            .FurnitureDefinitions.AsNoTracking()
            .Where(x => x.ProductType == ProductType.Floor || x.ProductType == ProductType.Wall)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.PublicName,
                x.SpriteId,
                x.ProductType,
                x.FurniLine,
                x.ClientCategory,
                x.FurniCategory,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return
        [
            .. rows.Where(x =>
                    !ProductStuffData.IsNamedByProduct(x.FurniCategory)
                    && !PetProductCodes.TryGetTypeId(x.Name, out _)
                )
                .Select(x => new CatalogAuditFurni(
                    x.Id,
                    x.Name,
                    string.IsNullOrWhiteSpace(x.PublicName) ? null : x.PublicName,
                    x.SpriteId,
                    AdminCatalogQueries.TypeName(x.ProductType),
                    string.IsNullOrWhiteSpace(x.FurniLine) ? null : x.FurniLine,
                    string.IsNullOrWhiteSpace(x.ClientCategory) ? null : x.ClientCategory
                )),
        ];
    }

    /// <summary>Every definition an offer gives, and whether any such offer is one players see.</summary>
    private static async Task<Dictionary<int, bool>> SoldAsync(
        TurboDbContext db,
        CancellationToken ct
    )
    {
        var pages = await PagesAsync(db, ct).ConfigureAwait(false);
        var products = await db
            .CatalogProducts.AsNoTracking()
            .Where(x => x.FurnitureDefinitionEntityId != null)
            .Select(x => new
            {
                Id = x.FurnitureDefinitionEntityId!.Value,
                x.Offer.Visible,
                x.Offer.CatalogPageEntityId,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var sold = new Dictionary<int, bool>();

        foreach (var product in products)
        {
            var shown = product.Visible && IsShown(pages, product.CatalogPageEntityId);

            sold[product.Id] = sold.GetValueOrDefault(product.Id) || shown;
        }

        return sold;
    }

    private sealed record PageRow(int Id, int? ParentId, string Title, CatalogPageDisplay Display);

    private static async Task<Dictionary<int, PageRow>> PagesAsync(
        TurboDbContext db,
        CancellationToken ct
    ) =>
        await db
            .CatalogPages.AsNoTracking()
            .Select(x => new PageRow(x.Id, x.ParentEntityId, x.Localization, x.Display))
            .ToDictionaryAsync(x => x.Id, ct)
            .ConfigureAwait(false);

    /// <summary>Whether players can reach a page: neither it nor a page above it is hidden.</summary>
    private static bool IsShown(Dictionary<int, PageRow> pages, int pageId)
    {
        var seen = new HashSet<int>();

        for (
            int? at = pageId;
            at is { } id && seen.Add(id);
            at = pages.GetValueOrDefault(id)?.ParentId
        )
        {
            if (
                !pages.TryGetValue(id, out var page)
                || page.Display == CatalogPageDisplay.Invisible
            )
                return false;
        }

        return true;
    }

    /// <summary>The titles of the pages above a page, from the tab down; the root is left out.</summary>
    private static string PathOf(Dictionary<int, PageRow> pages, int pageId)
    {
        var titles = new List<string>();
        var seen = new HashSet<int>();

        for (
            var page = pages.GetValueOrDefault(pageId);
            page?.ParentId is { } parentId && seen.Add(page.Id);
            page = pages.GetValueOrDefault(parentId)
        )
            titles.Add(page.Title);

        titles.Reverse();

        return string.Join(" / ", titles);
    }

    private static CatalogAuditFacet[] Facets(IEnumerable<string?> values) =>
        [
            .. values
                .GroupBy(x => x ?? string.Empty, StringComparer.Ordinal)
                .Select(g => new CatalogAuditFacet(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Value, StringComparer.Ordinal),
        ];
}
