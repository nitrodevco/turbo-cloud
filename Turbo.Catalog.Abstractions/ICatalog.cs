using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Catalog;

namespace Turbo.Catalog.Abstractions;

public interface ICatalog
{
    public CatalogTypeEnum CatalogType { get; }
    public ICatalogPage? RootPage { get; }
    public ValueTask LoadCatalogAsync(CancellationToken ct);
}
