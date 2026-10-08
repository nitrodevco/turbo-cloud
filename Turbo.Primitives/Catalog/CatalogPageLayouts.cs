using System;

namespace Turbo.Primitives.Catalog;

/// <summary>
/// Page layouts the server does something for besides sending the page: the front pages, which
/// draw the featured items (<c>layout_frontpage_featured.xml</c> embeds the client's
/// featured-items widget).
/// </summary>
public static class CatalogPageLayouts
{
    public const string FRONTPAGE4 = "frontpage4";
    public const string FRONTPAGE_FEATURED = "frontpage_featured";

    /// <summary>Whether a page of this layout shows the front page's featured items.</summary>
    public static bool ShowsFeaturedItems(string? layout) =>
        string.Equals(layout, FRONTPAGE4, StringComparison.Ordinal)
        || string.Equals(layout, FRONTPAGE_FEATURED, StringComparison.Ordinal);
}
