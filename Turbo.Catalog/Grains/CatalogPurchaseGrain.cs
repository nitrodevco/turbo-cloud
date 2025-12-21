using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Catalog.Exceptions;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Orleans;

namespace Turbo.Catalog.Grains;

public sealed partial class CatalogPurchaseGrain : Grain, ICatalogPurchaseGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly CatalogConfig _catalogConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ICatalogService _catalogService;

    private readonly CatalogPurchaseState _state;

    public CatalogPurchaseGrain(
        IDbContextFactory<TurboDbContext> dbContextFactory,
        IOptions<CatalogConfig> catalogConfig,
        IGrainFactory grainFactory,
        ICatalogService catalogService
    )
    {
        _dbCtxFactory = dbContextFactory;
        _catalogConfig = catalogConfig.Value;
        _grainFactory = grainFactory;
        _catalogService = catalogService;

        _state = new();
    }

    public override Task OnActivateAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public async Task<CatalogOfferSnapshot> PurchaseOfferFromCatalogAsync(
        CatalogType catalogType,
        int offerId,
        string extraParam,
        int quantity,
        CancellationToken ct
    )
    {
        var snapshot = _catalogService.GetCatalogSnapshot(catalogType);

        if (!snapshot.OffersById.TryGetValue(offerId, out var offer))
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.OfferNotFound);

        var inventory = _grainFactory.GetInventoryGrain(this.GetPrimaryKeyLong());

        await inventory.GrantCatalogOfferAsync(offer, extraParam, quantity, ct);

        return offer;
    }
}
