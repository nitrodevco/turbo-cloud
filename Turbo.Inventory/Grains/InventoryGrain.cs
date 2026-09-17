using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Inventory.Configuration;
using Turbo.Inventory.Grains.Modules;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Inventory.Grains;

/// <summary>
/// Owns a player's inventory. Furniture is hydrated lazily rather than on activation: the grain
/// is activated for cheap lookups too, and loading a full furniture list on every activation is
/// far more expensive than the first call that actually needs it. Every mutation writes through
/// to the database, so there is nothing to flush on deactivation.
/// </summary>
internal sealed partial class InventoryGrain : Grain, IInventoryGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly InventoryConfig _inventoryConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly IFurnitureDefinitionProvider _furnitureDefinitionProvider;
    private readonly IInventoryFurnitureLoader _furnitureItemsLoader;
    private readonly IStuffDataFactory _stuffDataFactory;
    private readonly ICatalogService _catalogService;
    private readonly ILogger<IInventoryGrain> _logger;

    private readonly InventoryLiveState _state;
    private readonly InventoryFurniModule _furniModule;

    public PlayerId PlayerId => _state.PlayerId;

    public InventoryGrain(
        IDbContextFactory<TurboDbContext> dbContextFactory,
        IOptions<InventoryConfig> inventoryConfig,
        IGrainFactory grainFactory,
        IFurnitureDefinitionProvider furnitureDefinitionProvider,
        IInventoryFurnitureLoader furnitureItemsLoader,
        IStuffDataFactory stuffDataFactory,
        ICatalogService catalogService,
        ILogger<IInventoryGrain> logger
    )
    {
        _dbCtxFactory = dbContextFactory;
        _inventoryConfig = inventoryConfig.Value;
        _grainFactory = grainFactory;
        _furnitureDefinitionProvider = furnitureDefinitionProvider;
        _furnitureItemsLoader = furnitureItemsLoader;
        _stuffDataFactory = stuffDataFactory;
        _catalogService = catalogService;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
        _furniModule = new InventoryFurniModule(this, _state, _furnitureItemsLoader);
    }
}
