using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;

namespace Turbo.Primitives.Catalog.Providers;

public interface ICatalogSnapshotProvider<TTag>
    where TTag : ICatalogTag
{
    public CatalogType CatalogType { get; }
    public CatalogSnapshot Current { get; }
    public Task<CatalogSnapshot> GetSnapshotAsync(CancellationToken ct);
    public Task ReloadAsync(CancellationToken ct);

    /// <summary>
    /// A sale from a limited series changed how many are left: the catalog shows the new count
    /// without being reloaded.
    /// </summary>
    public void SetLtdRemaining(int seriesId, int remaining);
}
