using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Catalog.Providers;

/// <summary>
/// One catalog cut from the tree of pages both catalogs share. A page is in it when its display
/// shows it there, and so is every page above it, so the client can reach it; a page that is
/// there only to lead to one below it sells nothing of its own.
/// <para>
/// The Builders Club catalog has no tabs: the client opens the first category under the root
/// and lists what is under that. So the tabs (the root's children) are left out, and the pages
/// under every tab are gathered, in tab order, under one category of its own.
/// </para>
/// </summary>
internal sealed record CatalogTree(
    int RootId,
    IReadOnlyList<CatalogPageEntity> Pages,
    IReadOnlySet<int> SellingPageIds,
    ImmutableDictionary<int, ImmutableArray<int>> ChildIds
)
{
    /// <summary>
    /// The Builders Club catalog's one category, which no row holds. Below -1, so the client
    /// asks for no page when it opens it, and apart from -1, which is "no parent".
    /// </summary>
    public const int BUILDERS_CLUB_CATEGORY_ID = -2;

    public static CatalogTree Cut(IReadOnlyCollection<CatalogPageEntity> all, CatalogType type)
    {
        // Should there be more than one page with no parent, the oldest is the catalog's; the
        // rest reach nothing.
        var root = all.Where(x => x.ParentEntityId is null).MinBy(x => x.Id);

        if (root is null)
            return new(
                -1,
                [],
                new HashSet<int>(),
                ImmutableDictionary<int, ImmutableArray<int>>.Empty
            );

        var byParent = all.Where(x => x.ParentEntityId is not null)
            .ToLookup(x => x.ParentEntityId!.Value);
        var pages = new List<CatalogPageEntity> { root };
        var selling = new HashSet<int>();
        var childIds = ImmutableDictionary.CreateBuilder<int, ImmutableArray<int>>();
        var buildersClub = type == CatalogType.BuildersClub;

        if (!buildersClub && root.Display.IsIn(type))
            selling.Add(root.Id);

        var tabs = Children(root.Id);

        if (!buildersClub)
        {
            childIds[root.Id] = [.. tabs.Where(Include).Select(x => x.Id)];

            return new(root.Id, pages, selling, childIds.ToImmutable());
        }

        // A tab is not shown, but the pages under it are, so it is walked like any other page
        // and then dropped; a tab that was only on the way leaves its pages to the category. An
        // invisible tab hides what is under it here too, as it does in the normal catalog.
        var gathered = new List<int>();

        foreach (var tab in tabs)
        {
            if (tab.Display == CatalogPageDisplay.Invisible || !Include(tab))
                continue;

            pages.Remove(tab);
            selling.Remove(tab.Id);
            gathered.AddRange(childIds[tab.Id]);
            childIds.Remove(tab.Id);
        }

        var category = new CatalogPageEntity
        {
            Id = BUILDERS_CLUB_CATEGORY_ID,
            ParentEntityId = root.Id,
            Localization = "Builders Club",
            Name = "builders_club",
            Icon = 0,
            Layout = root.Layout,
            SortOrder = 0,
            Display = CatalogPageDisplay.BuildersClubOnly,
        };

        foreach (var page in pages.Where(x => gathered.Contains(x.Id)))
            page.ParentEntityId = category.Id;

        pages.Add(category);
        childIds[root.Id] = [category.Id];
        childIds[category.Id] = [.. gathered];

        return new(root.Id, pages, selling, childIds.ToImmutable());

        List<CatalogPageEntity> Children(int parentId) =>
            [
                .. byParent[parentId]
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Localization)
                    .ThenBy(x => x.Id),
            ];

        // Whether the page is in this catalog, adding it and what of it is below when it is.
        bool Include(CatalogPageEntity page)
        {
            var shown = page.Display.IsIn(type);
            var kept = Children(page.Id).Where(Include).Select(x => x.Id).ToImmutableArray();

            if (!shown && kept.IsEmpty)
                return false;

            pages.Add(page);
            childIds[page.Id] = kept;

            if (shown)
                selling.Add(page.Id);

            return true;
        }
    }
}
