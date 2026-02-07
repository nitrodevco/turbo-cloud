using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Snapshots;

namespace Turbo.Primitives.Catalog.Providers;

public interface ICatalogCurrencyTypeProvider
{
    public Task<CatalogCurrencyTypeSnapshot?> GetCurrencyTypeByKeyAsync(
        string currencyKey,
        CancellationToken ct
    );

    public Task<CatalogCurrencyTypeSnapshot?> GetCurrencyTypeAsync(
        int currencyTypeId,
        CancellationToken ct
    );

    public Task ReloadAsync(CancellationToken ct);
}
