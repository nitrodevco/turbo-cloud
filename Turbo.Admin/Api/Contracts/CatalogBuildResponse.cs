namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// What applying a page builder did: the offers and pages it made, the offers it moved, the
/// edits now waiting to be published, and each item it could not make, with why.
/// </summary>
public sealed record CatalogBuildResponse(
    int OffersCreated,
    int PagesCreated,
    int OffersMoved,
    int UnpublishedChanges,
    CatalogBuildFailure[] Failures
);
