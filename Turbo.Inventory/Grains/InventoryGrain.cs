using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Inventory.Configuration;
using Turbo.Inventory.Grains.Modules;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Grains;

namespace Turbo.Inventory.Grains;

public sealed partial class InventoryGrain : Grain, IInventoryGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly InventoryConfig _inventoryConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly IInventoryFurnitureLoader _furnitureItemsLoader;

    private readonly InventoryLiveState _state;
    private readonly InventoryFurniModule _furniModule;

    public InventoryGrain(
        IDbContextFactory<TurboDbContext> dbContextFactory,
        IOptions<InventoryConfig> inventoryConfig,
        IGrainFactory grainFactory,
        IInventoryFurnitureLoader furnitureItemsLoader
    )
    {
        _dbCtxFactory = dbContextFactory;
        _inventoryConfig = inventoryConfig.Value;
        _grainFactory = grainFactory;
        _furnitureItemsLoader = furnitureItemsLoader;

        _state = new();
        _furniModule = new InventoryFurniModule(this, _state, _furnitureItemsLoader);
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
