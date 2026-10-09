namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// What applying a page builder did: the offers and pages it made, the offers it moved, the
/// edits now waiting to be published, each item it could not make, with why, and the page it
/// built onto - the new one, when it made one.
/// </summary>
public sealed record CatalogBuildResponse(
    int OffersCreated,
    int PagesCreated,
    int OffersMoved,
    int UnpublishedChanges,
    CatalogBuildFailure[] Failures,
    int? PageId = null
);
