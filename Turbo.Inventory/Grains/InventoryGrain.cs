using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Inventory.Configuration;
using Turbo.Inventory.Grains.Modules;
using Turbo.Primitives.Moderation;
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
    internal readonly IGrainFactory _grainFactory;
    internal readonly IWordFilter _wordFilter;
    private readonly ILogger<IInventoryGrain> _logger;
    internal readonly IAchievementFactRecorder _achievementFacts;

    private readonly InventoryLiveState _state;
    internal readonly InventoryFurniModule FurniModule;
    internal readonly InventoryPetModule PetModule;
    internal readonly InventoryBotModule BotModule;

    public PlayerId PlayerId => _state.PlayerId;

    public InventoryGrain(
        IDbContextFactory<TurboDbContext> dbContextFactory,
        IOptions<InventoryConfig> inventoryConfig,
        IGrainFactory grainFactory,
        IFurnitureDefinitionProvider furnitureDefinitionProvider,
        IInventoryFurnitureLoader furnitureItemsLoader,
        ICatalogService catalogService,
        IPetBreedProvider petBreedProvider,
        IWordFilter wordFilter,
        IAchievementFactRecorder achievementFacts,
        ILogger<IInventoryGrain> logger
    )
    {
        _inventoryConfig = inventoryConfig.Value;
        _grainFactory = grainFactory;
        _wordFilter = wordFilter;
        _logger = logger;
        _achievementFacts = achievementFacts;

        _state = new() { PlayerId = this.GetPlayerId() };
        FurniModule = new InventoryFurniModule(
            this,
            _state,
            dbContextFactory,
            furnitureItemsLoader,
            furnitureDefinitionProvider,
            catalogService,
            logger
        );
        PetModule = new InventoryPetModule(
            this,
            _state,
            dbContextFactory,
            petBreedProvider,
            logger
        );
        BotModule = new InventoryBotModule(this, _state, dbContextFactory, logger);
    }

    /// <summary>The presence that mirrors inventory changes to the player's client.</summary>
    internal IPlayerPresenceGrain Presence => _grainFactory.GetPlayerPresenceGrain(PlayerId);

    /// <summary>What the player has not looked at yet; told, never awaited.</summary>
    internal IPlayerUnseenItemsGrain UnseenItems =>
        _grainFactory.GetPlayerUnseenItemsGrain(PlayerId);

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
