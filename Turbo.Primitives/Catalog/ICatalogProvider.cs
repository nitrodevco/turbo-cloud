using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Primitives.Catalog;

public interface ICatalogProvider<TTag>
    where TTag : ICatalogTag
{
    public CatalogType CatalogType { get; }
    public CatalogSnapshot Current { get; }
    public Task ReloadAsync(CancellationToken ct = default);
}
