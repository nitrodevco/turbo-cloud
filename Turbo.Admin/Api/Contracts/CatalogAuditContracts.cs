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

/// <summary>
/// The pages to merge into the one named by the route, and the one of them whose offer stays
/// where two sell the same thing: the page kept when it is null.
/// </summary>
public sealed record CatalogPageMergeRequest(int[]? PageIds, int? PriorityPageId = null);

/// <summary>An offer of a merge's pages, where it is and what it costs.</summary>
public sealed record CatalogPageMergeOffer(
    int OfferId,
    int PageId,
    int CostCredits,
    int CostCurrency,
    int? CurrencyTypeId
);

/// <summary>A thing more than one of the pages sells: the offer that stays, and those deleted.</summary>
public sealed record CatalogPageMergeDuplicate(
    string Name,
    CatalogPageMergeOffer Kept,
    CatalogPageMergeOffer[] Dropped
);

/// <summary>What a merge would do: offers moved and deleted, pages moved and deleted, and how the page kept would be shown.</summary>
public sealed record CatalogPageMergePlan(
    int OffersMoved,
    int OffersDropped,
    int PagesMoved,
    int PagesDeleted,
    string Display,
    CatalogPageMergeDuplicate[] Duplicates
);

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

/// <summary>Every furni the unoffered list holds as narrowed, by id in its order, past its pages.</summary>
public sealed record CatalogUnofferedIdsResponse(int[] Ids);

/// <summary>
/// Floor and wall items to sell on a page, one offer each, at one price; with
/// <see cref="SubPages"/>, sorted onto a page under it for each kind of furni.
/// </summary>
public sealed record CatalogAddFurniRequest(
    int[]? DefinitionIds,
    int CostCredits,
    int CostCurrency,
    int? CurrencyTypeId,
    int ClubLevel,
    bool CanGift,
    bool Visible,
    bool SubPages = false
);

/// <summary>A page the furni would go on: one there already (<see cref="PageId"/>), or one to make.</summary>
public sealed record CatalogAddFurniPage(string Title, int? PageId, int Count);

/// <summary>The pages adding the furni would put them on, in the order they would be in.</summary>
public sealed record CatalogAddFurniPlan(CatalogAddFurniPage[] Pages);

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
