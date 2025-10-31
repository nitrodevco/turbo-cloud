using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Catalog.Abstractions;

public interface ICatalogProvider
{
    public CatalogSnapshot Current { get; }
    public Task ReloadAsync(CancellationToken ct = default);
}
