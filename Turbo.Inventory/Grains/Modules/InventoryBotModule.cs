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
/// The bots a player keeps in the inventory. The hand-over flow is
/// <see cref="InventoryUnitModule{TEntity, TSnapshot}"/>; a bot brings back the settings its
/// owner gave it in a room.
/// </summary>
internal sealed class InventoryBotModule(
    InventoryGrain inventoryGrain,
    InventoryLiveState liveState,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger logger
)
    : InventoryUnitModule<BotEntity, BotSnapshot>(
        inventoryGrain,
        liveState.Bots,
        dbCtxFactory,
        logger
    )
{
    protected override string Kind => "bot";

    protected override int MaxOwned => _inventoryGrain._inventoryConfig.MaxBots;

    protected override DbSet<BotEntity> Table(TurboDbContext dbCtx) => dbCtx.Bots;

    protected override BotSnapshot ToSnapshot(BotEntity entity, string ownerName) =>
        entity.ToSnapshot(ownerName);

    protected override BotSnapshot WithRoom(BotSnapshot snapshot, RoomId? roomId) =>
        snapshot with
        {
            RoomId = roomId,
        };

    protected override Task<int> WriteReturnedAsync(
        IQueryable<BotEntity> row,
        BotSnapshot snapshot,
        CancellationToken ct
    ) =>
        row.ExecuteUpdateAsync(
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

    protected override Task OnAddedAsync(
        BotSnapshot snapshot,
        bool openInventory,
        CancellationToken ct
    ) => _inventoryGrain.Presence.OnBotAddedAsync(snapshot, openInventory, ct);

    protected override Task OnRemovedAsync(int id, CancellationToken ct) =>
        _inventoryGrain.Presence.OnBotRemovedAsync(id, ct);

    public Task<BotSnapshot?> CreateAsync(
        string name,
        string motto,
        string figure,
        AvatarGenderType gender,
        CancellationToken ct
    ) =>
        CreateAsync(
            new BotEntity
            {
                PlayerEntityId = OwnerId,
                Name = name,
                Motto = motto,
                Figure = figure,
                Gender = gender,
                ChatDelaySeconds = _inventoryGrain._inventoryConfig.BotDefaultChatDelaySeconds,
            },
            ct
        );

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
