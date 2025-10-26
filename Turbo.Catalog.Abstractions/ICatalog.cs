using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Catalog.Abstractions;

public interface ICatalog
{
    public ValueTask LoadCatalogAsync(CancellationToken ct);
}
