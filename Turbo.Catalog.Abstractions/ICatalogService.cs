using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Catalog;

namespace Turbo.Catalog.Abstractions;

public interface ICatalogService
{
    public Task LoadCatalogAsync(CatalogTypeEnum catalogType, CancellationToken ct);
}
