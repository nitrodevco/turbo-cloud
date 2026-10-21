using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A floor or wall item as the editor's audits list it.</summary>
public sealed record CatalogAuditFurni(
    int Id,
    string Name,
    string? PublicName,
    int SpriteId,
    string Type,
    string? Line,
    string? Category
);

/// <summary>How many of the listed furni are in a furni line or a furnidata category.</summary>
public sealed record CatalogAuditFacet(string Value, int Count);

/// <summary>
/// Furni the catalog does not sell: in no offer at all (<c>missing</c>), or only in offers or on
/// pages players can't see (<c>hidden</c>). <see cref="Lines"/> and <see cref="Categories"/>
/// count the whole kind, before the text, line and category narrow it down.
/// </summary>
public sealed record CatalogUnofferedResponse(
    int Total,
    CatalogAuditFurni[] Items,
    CatalogAuditFacet[] Lines,
    CatalogAuditFacet[] Categories
);

/// <summary>An offer of a furni sold more than once, where it is and what it costs.</summary>
public sealed record CatalogDuplicateOffer(
    int OfferId,
    int PageId,
    string PageTitle,
    string PagePath,
    int CostCredits,
    int CostCurrency,
    int? CurrencyTypeId,
    bool Visible,
    bool Shown
);

/// <summary>A furni (with the pattern or number it carries, if any) and every offer that sells it alone.</summary>
public sealed record CatalogDuplicate(
    CatalogAuditFurni Furni,
    string? ExtraParam,
    CatalogDuplicateOffer[] Offers
);

public sealed record CatalogDuplicatesResponse(int Total, CatalogDuplicate[] Items);

/// <summary>A page of a name another page has too: where it is, what shows it, and what it holds.</summary>
public sealed record CatalogDuplicatePageItem(
    int PageId,
    string Title,
    string Path,
    string Display,
    bool Shown,
    int Offers,
    int Pages
);

/// <summary>The pages of one name, the oldest first.</summary>
public sealed record CatalogDuplicatePage(string Name, CatalogDuplicatePageItem[] Pages);

public sealed record CatalogDuplicatePagesResponse(int Total, CatalogDuplicatePage[] Items);

/// <summary>The pages to merge into the one named by the route.</summary>
public sealed record CatalogPageMergeRequest(int[]? PageIds);

/// <summary>
/// A merge done: offers moved to the page kept, those it gave already deleted, pages moved under
/// it, pages deleted, and how the page kept is shown now.
/// </summary>
public sealed record CatalogPageMergeResult(
    int OffersMoved,
    int OffersDropped,
    int PagesMoved,
    int PagesDeleted,
    string Display,
    int UnpublishedChanges
);

/// <summary>Floor and wall items to sell on a page, one offer each, at one price.</summary>
public sealed record CatalogAddFurniRequest(
    int[]? DefinitionIds,
    int CostCredits,
    int CostCurrency,
    int? CurrencyTypeId,
    int ClubLevel,
    bool CanGift,
    bool Visible
);

/// <summary>Offers to delete at once, as one step to undo.</summary>
public sealed record CatalogOffersDeleteRequest(int[]? OfferIds);

/// <summary>What a bulk edit did: how many it made, moved or deleted, and those it could not, by id.</summary>
public sealed record CatalogBulkResponse(
    int Done,
    int UnpublishedChanges,
    CatalogBuildFailure[] Failures
);

/// <summary>One step of the editor's history, as the panel lists it.</summary>
public sealed record CatalogHistoryItem(string Label, int EditorId, DateTime AtUtc, int Edits);

/// <summary>What can be undone (newest first) and redone (next first), and the edits waiting to go live.</summary>
public sealed record CatalogHistoryResponse(
    CatalogHistoryItem[] Undo,
    CatalogHistoryItem[] Redo,
    bool Truncated,
    int UnpublishedChanges
);
