using System;
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

    /// <summary>
    /// The steps since the last publish that can be undone, and those undone that can be done
    /// again. A restart or a publish starts it afresh.
    /// </summary>
    CatalogHistory History { get; }

    /// <summary>
    /// Runs <paramref name="edits"/> as one step of the history, named <paramref name="label"/>:
    /// a page built or a catalog generated is undone in one go, however many edits it took.
    /// </summary>
    Task<T> GroupAsync<T>(PlayerId editor, string label, Func<Task<T>> edits);

    /// <summary>
    /// Puts the newest step's rows back as they were before it, ids and order included; refused
    /// when one was changed since, or would take away what something now leans on.
    /// </summary>
    Task<CatalogEditResult> UndoAsync(PlayerId editor, CancellationToken ct);

    /// <summary>Does the step undone last again, as it left the rows.</summary>
    Task<CatalogEditResult> RedoAsync(PlayerId editor, CancellationToken ct);

    /// <summary>
    /// Undoes every step since the last publish, newest first, so the saved catalog is the one
    /// players have; stops at the first that can't be. Its id is how many steps were undone.
    /// They can be redone.
    /// </summary>
    Task<CatalogEditResult> DiscardAsync(PlayerId editor, CancellationToken ct);

    /// <summary>
    /// Makes and moves many pages and offers as one step, named <paramref name="label"/>: checked
    /// together first, so either all of it is saved or none of it and why.
    /// </summary>
    Task<CatalogTreeResult> BuildTreeAsync(
        PlayerId editor,
        string label,
        CatalogTreeDraft draft,
        CancellationToken ct
    );

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
