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
}
