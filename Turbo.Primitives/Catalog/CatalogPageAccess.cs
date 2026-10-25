using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Primitives.Catalog;

/// <summary>
/// Who may see a catalog page, and so open it and buy from it: a page with a
/// <see cref="CatalogPageSnapshot.MinRank"/> above the player's rank, or a
/// <see cref="CatalogPageSnapshot.RequiredNode"/> they do not hold, is not in their catalog at all,
/// and neither is anything under it. The same rules as a navigator category
/// (<see cref="NavigatorCategoryAccess"/>, <c>docs/permissions.md</c> §16).
/// <para>
/// Not in their catalog means not in the index the client is sent, rather than sent hidden: Habbo
/// sends a hidden page (<c>NodeData.visible</c> false) so a link can still open it by name
/// (NodeData.as:24), which is exactly what a page the player may not have must not allow.
/// </para>
/// </summary>
public static class CatalogPageAccess
{
    /// <summary>Whether the page's own gate lets the player in; the pages above it are not asked.</summary>
    public static bool CanSee(
        CatalogPageSnapshot page,
        ResolvedPermissionsSnapshot permissions,
        SecurityLevelType securityLevel
    ) => CanSee(page.MinRank, page.RequiredNode, permissions, securityLevel);

    /// <summary>Whether a gate of this rank and node lets the player in.</summary>
    public static bool CanSee(
        int minRank,
        string? requiredNode,
        ResolvedPermissionsSnapshot permissions,
        SecurityLevelType securityLevel
    ) =>
        minRank <= NavigatorCategoryAccess.RankOf(securityLevel)
        && (requiredNode is null || permissions.Has(requiredNode));

    /// <summary>Whether any page of the catalog asks more than being a player.</summary>
    public static bool IsGated(CatalogPageSnapshot page) =>
        page.MinRank > 1 || page.RequiredNode is not null;

    /// <summary>
    /// The catalog as a player sees it: every page <paramref name="allowed"/> refuses taken out
    /// with what is under it, and the offers and products of those pages with them, so a page,
    /// an offer or a purchase the player may not have is one the catalog does not know. The
    /// root is the index, not a page, and is always kept.
    /// </summary>
    public static CatalogSnapshot Cut(
        CatalogSnapshot catalog,
        Func<CatalogPageSnapshot, bool> allowed
    )
    {
        if (!catalog.PagesById.TryGetValue(catalog.RootPageId, out var root))
            return catalog;

        var kept = new Dictionary<int, CatalogPageSnapshot>();
        var pending = new Stack<CatalogPageSnapshot>();

        pending.Push(root);

        while (pending.TryPop(out var page))
        {
            var children = ImmutableArray.CreateBuilder<int>();

            foreach (var childId in page.ChildIds)
            {
                if (!catalog.PagesById.TryGetValue(childId, out var child) || !allowed(child))
                    continue;

                children.Add(childId);
                pending.Push(child);
            }

            kept[page.Id] =
                children.Count == page.ChildIds.Length
                    ? page
                    : page with
                    {
                        ChildIds = children.ToImmutable(),
                    };
        }

        if (kept.Count == catalog.PagesById.Count)
            return catalog;

        var offersById = catalog
            .OffersById.Where(x => kept.ContainsKey(x.Value.PageId))
            .ToImmutableDictionary();
        var productsById = catalog
            .ProductsById.Where(x => offersById.ContainsKey(x.Value.OfferId))
            .ToImmutableDictionary();
        var pageNames = kept.Values.Select(x => x.Name).OfType<string>().ToHashSet();
        var allNames = catalog.PagesById.Values.Select(x => x.Name).OfType<string>().ToHashSet();

        return catalog with
        {
            PagesById = kept.ToImmutableDictionary(),
            PageChildrenIds = kept.ToImmutableDictionary(x => x.Key, x => x.Value.ChildIds),
            PageOfferIds = catalog
                .PageOfferIds.Where(x => kept.ContainsKey(x.Key))
                .ToImmutableDictionary(),
            OffersById = offersById,
            ProductsById = productsById,
            OfferProductIds = catalog
                .OfferProductIds.Where(x => offersById.ContainsKey(x.Key))
                .ToImmutableDictionary(),
            ProductIdByLtdSeriesId = catalog
                .ProductIdByLtdSeriesId.Where(x => productsById.ContainsKey(x.Value))
                .ToImmutableDictionary(),
            // A featured item that leads to a page or an offer the player cannot have is not shown.
            FrontPageItems =
            [
                .. catalog.FrontPageItems.Where(x =>
                    (
                        x.CatalogPageLocation is not { } name
                        || !allNames.Contains(name)
                        || pageNames.Contains(name)
                    )
                    && (
                        x.ProductOfferId is not { } offerId
                        || !catalog.OffersById.ContainsKey(offerId)
                        || offersById.ContainsKey(offerId)
                    )
                ),
            ],
        };
    }
}
