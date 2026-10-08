namespace Turbo.Primitives.Catalog.Enums;

/// <summary>
/// What a featured item on the catalog's front page opens, as the client reads it after the
/// item's picture: the value that follows is a page name, an offer id or a product code.
/// </summary>
public enum CatalogFrontPageItemType
{
    /// <summary>A page, by the name the client opens it by.</summary>
    Page = 0,

    /// <summary>The page an offer is on, by the offer's id.</summary>
    Offer = 1,

    /// <summary>A product code; the client has no case for it and does nothing when it is pressed.</summary>
    Product = 2,
}
