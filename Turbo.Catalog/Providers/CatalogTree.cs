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
/// and lists what is under that. Habbo's root holds one real tab there, its "furni" tab, with
/// every page under it. So the first tab that leads to Builders Club pages is that category,
/// and the pages under the tabs after it are gathered under it too, in tab order, since the
/// client would never reach a second one.
/// </para>
/// </summary>
internal sealed record CatalogTree(
    int RootId,
    IReadOnlyList<CatalogPageEntity> Pages,
    IReadOnlySet<int> SellingPageIds,
    ImmutableDictionary<int, ImmutableArray<int>> ChildIds
)
{
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

        // Every tab is walked like any other page; the first one there is stays as the category
        // and the later ones hand it their pages and are dropped. An invisible tab hides what is
        // under it here too, as it does in the normal catalog.
        CatalogPageEntity? category = null;
        var gathered = new List<int>();

        foreach (var tab in tabs)
        {
            if (tab.Display == CatalogPageDisplay.Invisible || !Include(tab))
                continue;

            gathered.AddRange(childIds[tab.Id]);

            if (category is null)
            {
                category = tab;
                continue;
            }

            pages.Remove(tab);
            selling.Remove(tab.Id);
            childIds.Remove(tab.Id);
        }

        if (category is null)
        {
            childIds[root.Id] = [];

            return new(root.Id, pages, selling, childIds.ToImmutable());
        }

        foreach (var page in pages.Where(x => gathered.Contains(x.Id)))
            page.ParentEntityId = category.Id;

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
