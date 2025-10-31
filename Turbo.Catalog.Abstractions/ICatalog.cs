using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Catalog;

namespace Turbo.Catalog.Abstractions;

public interface ICatalog
{
    public CatalogTypeEnum CatalogType { get; }
    public ICatalogPage? RootPage { get; }

    public IDictionary<int, ICatalogPage> Pages { get; }
    public IDictionary<int, ICatalogOffer> Offers { get; }
    public IDictionary<int, ICatalogProduct> Products { get; }
    public ValueTask LoadCatalogAsync(CancellationToken ct);
}
