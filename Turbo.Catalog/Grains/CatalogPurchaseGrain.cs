using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Grains;

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
}
