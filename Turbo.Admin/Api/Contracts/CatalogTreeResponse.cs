namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// One catalog's pages, whether the viewer may change them, the edits not yet published, the
/// currencies a price can be in, the layouts a page can use, where page icons and images are
/// loaded from, and (for the normal catalog) its club shop.
/// </summary>
public sealed record CatalogTreeResponse(
    string CatalogType,
    int RootId,
    CatalogPageNode[] Pages,
    bool CanManage,
    int UnpublishedChanges,
    CatalogCurrencyItem[] Currencies,
    string[] Layouts,
    string ImageUrl,
    CatalogClubSummary? Club
);
