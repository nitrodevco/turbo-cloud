namespace Turbo.Primitives.Catalog.Enums;

/// <summary>
/// Which catalogs a page is shown in. Both catalogs are cut from the one tree of pages: a page
/// is in a catalog when it is shown there, and so is every page above it, so the client can reach
/// it (a page above one that is only there to lead to it lists none of its own offers).
/// </summary>
public enum CatalogPageDisplay
{
    /// <summary>The normal catalog only.</summary>
    Regular = 0,

    /// <summary>The Builders Club catalog only.</summary>
    BuildersClubOnly = 1,

    /// <summary>Both catalogs.</summary>
    Both = 2,

    /// <summary>
    /// Neither catalog's navigation. The normal catalog still holds it, hidden, so a link or a
    /// feature that opens it by name still finds it and its offers.
    /// </summary>
    Invisible = 3,
}

public static class CatalogPageDisplayExtensions
{
    /// <summary>Whether the page is shown in, and sells from, this catalog.</summary>
    public static bool IsIn(this CatalogPageDisplay display, CatalogType catalogType) =>
        catalogType switch
        {
            CatalogType.BuildersClub => display
                is CatalogPageDisplay.BuildersClubOnly
                    or CatalogPageDisplay.Both,
            _ => display is not CatalogPageDisplay.BuildersClubOnly,
        };

    /// <summary>Whether this display puts the page in the Builders Club catalog.</summary>
    public static bool IsInBuildersClub(this CatalogPageDisplay display) =>
        display.IsIn(CatalogType.BuildersClub);

    /// <summary>The name the panel uses: <c>regular</c>, <c>bc_only</c>, <c>both</c> or <c>invisible</c>.</summary>
    public static string ToName(this CatalogPageDisplay display) =>
        display switch
        {
            CatalogPageDisplay.BuildersClubOnly => "bc_only",
            CatalogPageDisplay.Both => "both",
            CatalogPageDisplay.Invisible => "invisible",
            _ => "regular",
        };

    public static CatalogPageDisplay? FromName(string? name) =>
        name?.Trim().ToLowerInvariant() switch
        {
            "regular" => CatalogPageDisplay.Regular,
            "bc_only" => CatalogPageDisplay.BuildersClubOnly,
            "both" => CatalogPageDisplay.Both,
            "invisible" => CatalogPageDisplay.Invisible,
            _ => null,
        };
}
