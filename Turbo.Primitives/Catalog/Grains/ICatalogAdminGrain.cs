using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Catalog.Admin;

namespace Turbo.Primitives.Catalog.Grains;

/// <summary>
/// Owns admin mutations to catalog page/offer/product structure. Unlike <see cref="ICatalogPurchaseGrain"/>
/// (transactional purchases) and <see cref="ICatalogLtdRaffleGrain"/> (raffle queue), this grain exists purely
/// to give the out-of-process admin panel a way to write catalog rows and then reload the in-process
/// <c>ICatalogSnapshotProvider</c> cache in the same call, so edits take effect for connected players
/// immediately instead of only on the next emulator restart.
/// </summary>
public interface ICatalogAdminGrain : IGrainWithStringKey
{
    public Task<int> CreatePageAsync(CreateCatalogPageInput input, CancellationToken ct);
    public Task UpdatePageAsync(int pageId, UpdateCatalogPageInput input, CancellationToken ct);
    public Task DeletePageAsync(int pageId, CancellationToken ct);
    public Task ReorderPagesAsync(List<CatalogPageOrderEntry> entries, CancellationToken ct);

    public Task<int> CreateOfferAsync(CreateCatalogOfferInput input, CancellationToken ct);
    public Task UpdateOfferAsync(int offerId, UpdateCatalogOfferInput input, CancellationToken ct);
    public Task DeleteOfferAsync(int offerId, CancellationToken ct);

    public Task<int> CreateProductAsync(CreateCatalogProductInput input, CancellationToken ct);
    public Task UpdateProductAsync(
        int productId,
        UpdateCatalogProductInput input,
        CancellationToken ct
    );
    public Task DeleteProductAsync(int productId, CancellationToken ct);
}
