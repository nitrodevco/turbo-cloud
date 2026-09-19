using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Inventory.Configuration;
using Turbo.Inventory.Grains.Modules;
using Turbo.Primitives.Badges.Grains;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;

namespace Turbo.Inventory.Grains;

/// <summary>
/// Owns a player's inventory. Furniture, pets and bots are hydrated lazily rather than on
/// activation: the grain is activated for cheap lookups too, and loading full lists on every
/// activation is far more expensive than the first call that actually needs them. Every
/// mutation writes through to the database, so there is nothing to flush on deactivation.
/// Each section is a module with the same shape (ensure ready, read, add, remove); the grain
/// partials only forward to them.
/// </summary>
internal sealed partial class InventoryGrain : Grain, IInventoryGrain
{
    internal readonly InventoryConfig _inventoryConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IInventoryGrain> _logger;

    private readonly InventoryLiveState _state;
    private readonly InventoryFurniModule _furniModule;
    private readonly InventoryPetModule _petModule;
    private readonly InventoryBotModule _botModule;
    private readonly InventoryBadgeModule _badgeModule;

    public PlayerId PlayerId => _state.PlayerId;

    public InventoryGrain(
        IDbContextFactory<TurboDbContext> dbContextFactory,
        IOptions<InventoryConfig> inventoryConfig,
        IGrainFactory grainFactory,
        IFurnitureDefinitionProvider furnitureDefinitionProvider,
        IInventoryFurnitureLoader furnitureItemsLoader,
        ICatalogService catalogService,
        IPetBreedProvider petBreedProvider,
        ILogger<IInventoryGrain> logger
    )
    {
        _inventoryConfig = inventoryConfig.Value;
        _grainFactory = grainFactory;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
        _furniModule = new InventoryFurniModule(
            this,
            _state,
            dbContextFactory,
            furnitureItemsLoader,
            furnitureDefinitionProvider,
            catalogService,
            logger
        );
        _petModule = new InventoryPetModule(
            this,
            _state,
            dbContextFactory,
            petBreedProvider,
            logger
        );
        _botModule = new InventoryBotModule(this, _state, dbContextFactory, logger);
        _badgeModule = new InventoryBadgeModule(this, _state, dbContextFactory, logger);
    }

    /// <summary>The presence that mirrors inventory changes to the player's client.</summary>
    internal IPlayerPresenceGrain Presence => _grainFactory.GetPlayerPresenceGrain(PlayerId);

    internal IBadgeDirectoryGrain BadgeDirectory => _grainFactory.GetBadgeDirectoryGrain();

    internal IBadgeLeaderboardGrain BadgeLeaderboard => _grainFactory.GetBadgeLeaderboardGrain();

    internal IPlayerGrain Player => _grainFactory.GetPlayerGrain(PlayerId);

    internal IInventoryGrain GetInventoryOf(PlayerId playerId) =>
        _grainFactory.GetInventoryGrain(playerId);

    /// <summary>
    /// The owner's name every furniture, pet and bot snapshot carries. Asked of the directory
    /// once per activation instead of once per load or purchase.
    /// </summary>
    internal async ValueTask<string> GetOwnerNameAsync(CancellationToken ct) =>
        _state.OwnerName ??= await _grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNameAsync(PlayerId, ct);
}
