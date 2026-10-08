using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Catalog.Editing;

/// <summary>
/// Changing the catalog: its pages and offers, saved to the database as each edit is made, and
/// put in front of players by <see cref="PublishAsync"/>, which reloads the catalogs and tells
/// every client online to refresh. Each edit is checked first, so the hotel never sells what it
/// cannot hand out, and refused with a reason when it would break something.
/// </summary>
public interface ICatalogEditService
{
    /// <summary>
    /// Edits saved since the last publish. A <c>:reload catalog</c> puts them live too, without
    /// counting; a restart starts at none, the catalogs being loaded fresh.
    /// </summary>
    int UnpublishedChanges { get; }

    Task<CatalogEditResult> CreatePageAsync(
        PlayerId editor,
        int parentId,
        CatalogPageDraft draft,
        CancellationToken ct
    );

    Task<CatalogEditResult> UpdatePageAsync(
        PlayerId editor,
        int pageId,
        CatalogPageDraft draft,
        CancellationToken ct
    );

    /// <summary>Puts a page under <paramref name="parentId"/> at <paramref name="index"/> among its children.</summary>
    Task<CatalogEditResult> MovePageAsync(
        PlayerId editor,
        int pageId,
        int parentId,
        int index,
        CancellationToken ct
    );

    /// <summary>Deletes an empty page: one with no pages under it and no offers on it.</summary>
    Task<CatalogEditResult> DeletePageAsync(PlayerId editor, int pageId, CancellationToken ct);

    Task<CatalogEditResult> CreateOfferAsync(
        PlayerId editor,
        CatalogOfferDraft draft,
        CancellationToken ct
    );

    Task<CatalogEditResult> UpdateOfferAsync(
        PlayerId editor,
        int offerId,
        CatalogOfferDraft draft,
        CancellationToken ct
    );

    /// <summary>
    /// Puts an offer on <paramref name="pageId"/> at <paramref name="index"/> among its offers,
    /// the same page to reorder it; both pages' offers are numbered afresh in their order.
    /// </summary>
    Task<CatalogEditResult> MoveOfferAsync(
        PlayerId editor,
        int offerId,
        int pageId,
        int index,
        CancellationToken ct
    );

    Task<CatalogEditResult> DeleteOfferAsync(PlayerId editor, int offerId, CancellationToken ct);

    /// <summary>
    /// Replaces the front page's featured items with these, placed in this order from 1.
    /// Saved with the id 0, there being no one row that was saved.
    /// </summary>
    Task<CatalogEditResult> SaveFeaturedItemsAsync(
        PlayerId editor,
        IReadOnlyList<CatalogFeaturedItemDraft> items,
        CancellationToken ct
    );

    /// <summary>
    /// Makes an offer of one floor or wall item a limited series, or changes its series. A new
    /// total moves what is left by as much, and is never below what is sold.
    /// </summary>
    Task<CatalogEditResult> SaveLimitedAsync(
        PlayerId editor,
        int offerId,
        CatalogLimitedDraft draft,
        CancellationToken ct
    );

    /// <summary>Takes the series off an offer, while none of it is sold or raffled.</summary>
    Task<CatalogEditResult> RemoveLimitedAsync(PlayerId editor, int offerId, CancellationToken ct);

    Task<CatalogPublishResult> PublishAsync(PlayerId editor, CancellationToken ct);
}
