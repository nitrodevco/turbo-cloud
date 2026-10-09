using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Hotel.Snapshots;

namespace Turbo.Primitives.Hotel;

/// <summary>
/// The reception's promo articles: what players see now, and the editing staff do in the panel.
/// </summary>
public interface IPromoArticleService
{
    /// <summary>
    /// The articles players see now, in order: visible, within their dates, up to the configured
    /// count. Kept for a short while, so another silo sees an edit within that time.
    /// </summary>
    public Task<ImmutableArray<PromoArticleSnapshot>> GetLiveAsync(CancellationToken ct);

    /// <summary>Every article, in order, whether players see it or not.</summary>
    public Task<ImmutableArray<PromoArticleSnapshot>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Adds an article (id 0) at the end, or changes the one of that id; the order is kept. Throws
    /// <see cref="System.ArgumentException"/> for an empty title, a text too long to keep, an end
    /// before the start, or an id there is no article of.
    /// </summary>
    public Task<PromoArticleSnapshot> SaveAsync(PromoArticleSnapshot article, CancellationToken ct);

    /// <summary>Removes an article; false when there is none.</summary>
    public Task<bool> DeleteAsync(int id, CancellationToken ct);

    /// <summary>
    /// Puts the articles in the order given; any not named keep their order after them.
    /// </summary>
    public Task ReorderAsync(IReadOnlyList<int> ids, CancellationToken ct);
}
