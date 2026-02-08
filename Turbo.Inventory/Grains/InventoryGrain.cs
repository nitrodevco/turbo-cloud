using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Inventory.Configuration;
using Turbo.Inventory.Grains.Modules;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Grains;

namespace Turbo.Inventory.Grains;

public sealed partial class InventoryGrain : Grain, IInventoryGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly InventoryConfig _inventoryConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly IFurnitureDefinitionProvider _furnitureDefinitionProvider;
    private readonly IInventoryFurnitureLoader _furnitureItemsLoader;
    private readonly IStuffDataFactory _stuffDataFactory;
    private readonly ICatalogService _catalogService;

    private readonly InventoryLiveState _state;
    private readonly InventoryFurniModule _furniModule;

    public InventoryGrain(
        IDbContextFactory<TurboDbContext> dbContextFactory,
        IOptions<InventoryConfig> inventoryConfig,
        IGrainFactory grainFactory,
        IFurnitureDefinitionProvider furnitureDefinitionProvider,
        IInventoryFurnitureLoader furnitureItemsLoader,
        IStuffDataFactory stuffDataFactory,
        ICatalogService catalogService
    )
    {
        _dbCtxFactory = dbContextFactory;
        _inventoryConfig = inventoryConfig.Value;
        _grainFactory = grainFactory;
        _furnitureDefinitionProvider = furnitureDefinitionProvider;
        _furnitureItemsLoader = furnitureItemsLoader;
        _stuffDataFactory = stuffDataFactory;
        _catalogService = catalogService;

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
