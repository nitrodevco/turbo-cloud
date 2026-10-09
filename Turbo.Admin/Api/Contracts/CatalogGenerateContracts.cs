namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A whole catalog to generate. <see cref="Mode"/> is <c>replace</c> (the tabs there now go,
/// with all under them, into one hidden tab, and the offers the new pages sell are moved onto
/// them where <see cref="ReuseOffers"/> asks) or <c>alongside</c> (the new tabs come hidden,
/// after the old, every offer made new). <see cref="Sections"/> picks the tabs; null is every
/// one. New offers cost <see cref="CostCredits"/> and the currency price; a page sells
/// <see cref="MaxPerPage"/> at most, a furni line with more spread over numbered pages.
/// <see cref="Edits"/> retitle, re-icon or leave out planned pages by their key.
/// </summary>
public sealed record CatalogGenerateRequest(
    string? Mode,
    string[]? Sections,
    bool ReuseOffers,
    int MaxPerPage,
    int CostCredits,
    int CostCurrency,
    int? CurrencyTypeId,
    CatalogGeneratePageEdit[]? Edits
);

/// <summary>A change to one planned page: a title, an icon, or leaving it (and all under it) out.</summary>
public sealed record CatalogGeneratePageEdit(string Key, string? Title, int? Icon, bool Skip);

/// <summary>A page the generator plans: what it is, what it shows, and what it will sell, made new or moved there.</summary>
public sealed record CatalogGeneratedPage(
    string Key,
    string Section,
    string Title,
    string? Name,
    int Icon,
    string Layout,
    string Display,
    int NewOffers,
    int MovedOffers,
    CatalogGeneratedPage[] Children
);

/// <summary>The generated catalog as it would be, with its totals and what to look at before making it.</summary>
public sealed record CatalogGeneratePlan(
    CatalogGeneratedPage[] Tabs,
    string[] Warnings,
    int Pages,
    int NewOffers,
    int MovedOffers,
    int ArchivedTabs
);

/// <summary>What generating made, and the edits now waiting to be published.</summary>
public sealed record CatalogGenerateResponse(
    int Pages,
    int OffersCreated,
    int OffersMoved,
    int PagesMoved,
    int UnpublishedChanges
);
