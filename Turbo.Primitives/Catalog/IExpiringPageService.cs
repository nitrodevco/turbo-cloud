using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Snapshots;

namespace Turbo.Primitives.Catalog;

/// <summary>
/// The catalog pages the reception's expiring page widget counts down to, and the one it shows:
/// the first to run out among those still to come.
/// </summary>
public interface IExpiringPageService
{
    /// <summary>The page that runs out first among those still to come; null when none does.</summary>
    public Task<CatalogPageExpirySnapshot?> GetEarliestAsync(CancellationToken ct);

    /// <summary>Every page with an expiry, the first to run out first.</summary>
    public Task<ImmutableArray<CatalogPageExpirySnapshot>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Sets a page's expiry, adding one or replacing the one it has. Throws
    /// <see cref="System.ArgumentException"/> for a page there isn't or one without a name the
    /// client can open it by.
    /// </summary>
    public Task<CatalogPageExpirySnapshot> SaveAsync(
        int pageId,
        System.DateTime expiresAt,
        string image,
        CancellationToken ct
    );

    /// <summary>Takes a page's expiry away; false when it has none.</summary>
    public Task<bool> DeleteAsync(int pageId, CancellationToken ct);
}
