namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The catalog's pages (both catalogs are cut from them, by each page's display), whether the
/// viewer may change them, the edits not yet published, the currencies a price can be in, the
/// layouts a page can use, and the club shop.
/// </summary>
public sealed record CatalogTreeResponse(
    int RootId,
    CatalogPageNode[] Pages,
    bool CanManage,
    int UnpublishedChanges,
    CatalogCurrencyItem[] Currencies,
    string[] Layouts,
    CatalogClubSummary Club
);
