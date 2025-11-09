using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Catalog;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Primitives.Catalog;

public interface ICatalogProvider<TTag>
    where TTag : ICatalogTag
{
    public CatalogTypeEnum CatalogType { get; }
    public CatalogSnapshot Current { get; }
    public Task ReloadAsync(CancellationToken ct = default);
}
