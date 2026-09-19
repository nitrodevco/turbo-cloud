using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Bots;
using Turbo.Database.Extensions;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Inventory.Grains.Modules;

/// <summary>
/// The bots a player keeps in the inventory (not standing in a room). Same hand-over rules as
/// <see cref="InventoryPetModule"/>: the row moves first, the list follows.
/// </summary>
internal sealed class InventoryBotModule(
    InventoryGrain inventoryGrain,
    InventoryLiveState liveState,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger logger
)
{
    private readonly InventoryGrain _inventoryGrain = inventoryGrain;
    private readonly InventoryLiveState _state = liveState;
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ILogger _logger = logger;

    private int OwnerId => (int)_inventoryGrain.PlayerId;

    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        if (_state.IsBotsReady)
            return;

        var ownerName = await _inventoryGrain.GetOwnerNameAsync(ct);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx
            .Bots.AsNoTracking()
            .Where(x => x.PlayerEntityId == OwnerId && x.RoomEntityId == null)
            .ToListAsync(ct);

        _state.BotsById.Clear();

        foreach (var entity in entities)
            _state.BotsById[entity.Id] = entity.ToSnapshot(ownerName);

        _state.IsBotsReady = true;
    }

    public async Task<ImmutableArray<BotSnapshot>> GetAllAsync(CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        return [.. _state.BotsById.Values];
    }

    public async Task<BotSnapshot?> GetAsync(int botId, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        return _state.BotsById.TryGetValue(botId, out var bot) ? bot : null;
    }

    /// <summary>Hands a bot to a room. Null when it is not here to give.</summary>
    public async Task<BotSnapshot?> TryCheckOutAsync(int botId, RoomId roomId, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        if (!_state.BotsById.TryGetValue(botId, out var bot))
            return null;

        int updated;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            updated = await dbCtx
                .Bots.Where(x =>
                    x.Id == botId && x.PlayerEntityId == OwnerId && x.RoomEntityId == null
                )
                .ExecuteUpdateAsync(up => up.SetProperty(p => p.RoomEntityId, roomId.Value), ct);
        }

        if (updated == 0)
        {
            _logger.LogWarning(
                "Bot {BotId} of player {PlayerId} is listed in the inventory but its row is elsewhere; reloading",
                botId,
                _inventoryGrain.PlayerId
            );

            _state.IsBotsReady = false;

            return null;
        }

        _state.BotsById.Remove(botId);

        await _inventoryGrain.Presence.OnBotRemovedAsync(botId, ct);

        return bot with
        {
            RoomId = roomId,
        };
    }

    /// <summary>Takes a bot back from a room, with the settings it was given there.</summary>
    public async Task<bool> ReturnAsync(BotSnapshot snapshot, CancellationToken ct)
    {
        if (snapshot.OwnerId != _inventoryGrain.PlayerId)
            return false;

        await EnsureReadyAsync(ct);

        int updated;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            updated = await dbCtx
                .Bots.Where(x => x.Id == snapshot.Id && x.PlayerEntityId == OwnerId)
                .ExecuteUpdateAsync(
                    up =>
                        up.SetProperty(p => p.RoomEntityId, (int?)null)
                            .SetProperty(p => p.Name, snapshot.Name)
                            .SetProperty(p => p.Motto, snapshot.Motto)
                            .SetProperty(p => p.Figure, snapshot.Figure)
                            .SetProperty(p => p.Gender, snapshot.Gender)
                            .SetProperty(p => p.FreeRoam, snapshot.FreeRoam)
                            .SetProperty(p => p.ChatText, snapshot.ChatText)
                            .SetProperty(p => p.AutoChat, snapshot.AutoChat)
                            .SetProperty(p => p.ChatDelaySeconds, snapshot.ChatDelaySeconds)
                            .SetProperty(p => p.MixSentences, snapshot.MixSentences)
                            .SetProperty(p => p.DanceType, snapshot.DanceType),
                    ct
                );
        }

        if (updated == 0)
        {
            _logger.LogError(
                "Bot {BotId} returned to player {PlayerId} has no row to update",
                snapshot.Id,
                _inventoryGrain.PlayerId
            );

            return false;
        }

        var returned = snapshot with { RoomId = null };

        _state.BotsById[returned.Id] = returned;

        await _inventoryGrain.Presence.OnBotAddedAsync(returned, false, ct);

        return true;
    }

    /// <summary>
    /// Creates a bot in the inventory. Null when the player already owns the configured
    /// maximum, counting the bots standing in rooms.
    /// </summary>
    public async Task<BotSnapshot?> CreateAsync(
        string name,
        string motto,
        string figure,
        AvatarGenderType gender,
        CancellationToken ct
    )
    {
        await EnsureReadyAsync(ct);

        var config = _inventoryGrain._inventoryConfig;
        var entity = new BotEntity
        {
            PlayerEntityId = OwnerId,
            Name = name,
            Motto = motto,
            Figure = figure,
            Gender = gender,
            ChatDelaySeconds = config.BotDefaultChatDelaySeconds,
        };

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var owned = await dbCtx.Bots.CountAsync(x => x.PlayerEntityId == OwnerId, ct);

            if (owned >= config.MaxBots)
            {
                _logger.LogWarning(
                    "Player {PlayerId} owns {Count} bots, the configured maximum; not creating another",
                    _inventoryGrain.PlayerId,
                    owned
                );

                return null;
            }

            dbCtx.Add(entity);

            await dbCtx.SaveChangesAsync(ct);
        }

        var snapshot = entity.ToSnapshot(await _inventoryGrain.GetOwnerNameAsync(ct));

        _state.BotsById[snapshot.Id] = snapshot;

        await _inventoryGrain.Presence.OnBotAddedAsync(snapshot, true, ct);

        return snapshot;
    }

    public async Task<bool> DeleteAsync(int botId, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        int deleted;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            deleted = await dbCtx
                .Bots.Where(x => x.Id == botId && x.PlayerEntityId == OwnerId)
                .ExecuteDeleteAsync(ct);
        }

        if (deleted == 0)
            return false;

        if (_state.BotsById.Remove(botId))
            await _inventoryGrain.Presence.OnBotRemovedAsync(botId, ct);

        return true;
    }

    /// <summary>
    /// Checks a bot product before anything is created. Its extra param is the figure the
    /// catalog renders the bot's head from; its class name, when set, is the bot's name.
    /// </summary>
    public BotProductGrant ValidateProduct(
        CatalogOfferSnapshot offer,
        CatalogProductSnapshot product
    )
    {
        if (string.IsNullOrWhiteSpace(product.ExtraParam))
        {
            _logger.LogError(
                "Bot product {ProductId} of offer {OfferId} has no figure; cannot grant it to player {PlayerId}",
                product.Id,
                offer.Id,
                _inventoryGrain.PlayerId
            );

            throw new TurboException(TurboErrorCodeEnum.CatalogProductNotFound);
        }

        var name = string.IsNullOrWhiteSpace(product.ClassName)
            ? _inventoryGrain._inventoryConfig.BotDefaultName
            : product.ClassName;

        return new BotProductGrant(name, product.ExtraParam);
    }

    public async Task GrantProductAsync(BotProductGrant grant, CancellationToken ct)
    {
        var bot = await CreateAsync(
            grant.Name,
            _inventoryGrain._inventoryConfig.BotDefaultMotto,
            grant.Figure,
            AvatarGenderType.Male,
            ct
        );

        if (bot is null)
            throw new TurboException(TurboErrorCodeEnum.CatalogProductNotFound);
    }
}
