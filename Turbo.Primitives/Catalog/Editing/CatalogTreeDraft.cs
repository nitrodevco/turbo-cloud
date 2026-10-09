using System.Collections.Generic;

namespace Turbo.Primitives.Catalog.Editing;

/// <summary>
/// A page in a <see cref="CatalogTreeDraft"/>: one saved already, by <see cref="Id"/>, or the
/// page at <see cref="New"/> among the draft's own new pages, made before anything refers to it.
/// </summary>
public sealed record CatalogPageRef(int? Id, int? New)
{
    public static CatalogPageRef Saved(int id) => new(id, null);

    public static CatalogPageRef Made(int index) => new(null, index);
}

/// <summary>
/// A page to make under <see cref="Parent"/>: last among its pages, or at <see cref="Index"/>
/// among the pages already there.
/// </summary>
public sealed record CatalogNewPage(
    CatalogPageRef Parent,
    CatalogPageDraft Page,
    int? Index = null
);

/// <summary>
/// An offer to make on <see cref="Page"/>, after what is there; the draft's own page id is not
/// read. Memberships and club gifts are made one at a time, not in a tree.
/// </summary>
public sealed record CatalogNewOffer(CatalogPageRef Page, CatalogOfferDraft Offer);

/// <summary>A saved offer to move to the end of <see cref="Page"/>, as it is.</summary>
public sealed record CatalogOfferPlacement(int OfferId, CatalogPageRef Page);

/// <summary>A saved page to move, with what is under it, to the end of <see cref="Parent"/>'s pages.</summary>
public sealed record CatalogPagePlacement(int PageId, CatalogPageRef Parent);

/// <summary>
/// Many edits made as one, checked together and saved together, and undone as one step: the
/// catalog generator's whole tree, or many furni put on a page at once. New pages come first,
/// in order, then saved pages are moved, then new offers are made and saved offers moved, each
/// going last on its page in the order given.
/// </summary>
public sealed record CatalogTreeDraft(
    IReadOnlyList<CatalogNewPage> Pages,
    IReadOnlyList<CatalogNewOffer> Offers,
    IReadOnlyList<CatalogOfferPlacement> OfferMoves,
    IReadOnlyList<CatalogPagePlacement> PageMoves
);

/// <summary>What a <see cref="CatalogTreeDraft"/> made, with the ids of its new pages in its order; or why nothing was.</summary>
public sealed record CatalogTreeResult(
    string? Error,
    int OffersCreated,
    int OffersMoved,
    int PagesMoved,
    IReadOnlyList<int> PageIds
)
{
    public bool Saved => Error is null;

    public static CatalogTreeResult Refused(string error) => new(error, 0, 0, 0, []);
}
