using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomActionModule(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    public Task<bool> AddItemAsync(IRoomItem item, CancellationToken ct) =>
        _roomGrain.ObjectModule.AttatchObjectAsync(item, ct);

    public async Task<bool> RemoveItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        // Nobody owns a temporary furni, so nobody picks one up into an inventory.
        if (item.IsTemporary)
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        // A borrowed furni is given back rather than picked up: it belongs to the club, so it
        // reaches no inventory and the room simply lets go of it. The client warns the player
        // that it cannot be borrowed again before it asks for this.
        if (item.IsBuildersClub)
        {
            if (
                await _roomGrain.SecurityModule.GetControllerLevelAsync(ctx)
                < RoomControllerType.GroupAdmin
            )
                throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

            return await _roomGrain.ObjectModule.RemoveObjectAsync(ctx, item, ct, ctx.PlayerId);
        }

        var pickupType = await _roomGrain.SecurityModule.GetFurniPickupTypeAsync(ctx);

        // Whatever a player may do in the room, their own furni is theirs to take back: someone
        // who built on a rented space, or whose rights were taken away, is not stuck with it here.
        if (pickupType == FurniturePickupType.None && item.OwnerId == ctx.PlayerId)
            pickupType = FurniturePickupType.SendToOwner;

        if (pickupType == FurniturePickupType.None)
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        PlayerId pickerId = item.OwnerId;

        if (pickupType is FurniturePickupType.SendToCtx)
            pickerId = ctx.PlayerId;

        item.SetOwnerId(pickerId);

        await _roomGrain.ObjectModule.RemoveObjectAsync(ctx, item, ct, pickerId);

        var snapshot = item.GetSnapshot();

        var inventory = _roomGrain._grainFactory.GetInventoryGrain(snapshot.OwnerId);

        await inventory.AddFurnitureFromRoomItemSnapshotAsync(snapshot, ct);

        return true;
    }

    /// <summary>
    /// Sends many items home at once: the room lets go of all of them first, then each owner's
    /// inventory takes its share in one call, owners side by side. The room is told once per
    /// owner for floor items (the client removes them together); wall items have no such
    /// message and go one by one.
    /// </summary>
    public async Task ReturnItemsToOwnersAsync(
        IReadOnlyCollection<IRoomItem> items,
        CancellationToken ct
    )
    {
        if (items.Count == 0)
            return;

        var ctx = ActionContext.CreateForSystem(_roomGrain.RoomId);
        var returned = new Dictionary<PlayerId, List<IRoomItem>>();
        var borrowsGivenBack = new Dictionary<PlayerId, int>();

        foreach (var item in items)
        {
            var isFloorItem = item is IRoomFloorItem;

            if (
                !await _roomGrain.ObjectModule.RemoveObjectAsync(
                    ctx,
                    item,
                    ct,
                    item.OwnerId,
                    announce: !isFloorItem,
                    reportBorrow: false
                )
            )
                continue;

            // A borrowed furni goes back to the club rather than to anybody's inventory. They
            // are counted up here so each borrower is told their new total once.
            if (item.IsBuildersClub)
            {
                borrowsGivenBack[item.OwnerId] =
                    borrowsGivenBack.GetValueOrDefault(item.OwnerId) + 1;

                continue;
            }

            // A temporary furni leaves the room like any other and then is simply gone.
            if (item.IsTemporary)
                continue;

            if (!returned.TryGetValue(item.OwnerId, out var owned))
                returned[item.OwnerId] = owned = [];

            owned.Add(item);
        }

        foreach (var (ownerId, owned) in returned)
        {
            var floorIds = owned
                .Where(x => x is IRoomFloorItem)
                .Select(x => (long)x.ObjectId.Value)
                .ToList();

            if (floorIds.Count > 0)
                await _roomGrain.SendComposerToRoomAsync(
                    new ObjectRemoveMultipleMessageComposer
                    {
                        ObjectIdsToRemove = [.. floorIds],
                        PickerId = ownerId,
                    },
                    ct
                );
        }

        var buildersClub = _roomGrain._grainFactory.GetBuildersClubGrain();

        foreach (var (playerId, count) in borrowsGivenBack)
            await buildersClub.OnReturnedAsync(playerId, count, ct);

        await Task.WhenAll(
            returned.Select(entry =>
                _roomGrain
                    ._grainFactory.GetInventoryGrain(entry.Key)
                    .AddFurnitureFromRoomItemSnapshotsAsync(
                        [.. entry.Value.Select(x => x.GetSnapshot())],
                        ct
                    )
            )
        );
    }

    public async Task<bool> UseItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (!await item.Logic.CanUseAsync(ctx))
            return false;

        await item.Logic.OnUseAsync(ctx, param, ct);

        await _roomGrain.PublishRoomEventAsync(
            new RoomItemUsedEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ctx,
                ObjectId = item.ObjectId,
            },
            ct
        );

        return true;
    }

    /// <summary>
    /// Dedicated furniture actions carry their own permission rules (owner-only edits, rights,
    /// adjacency), so the logic checks them rather than the item usage policy.
    /// </summary>
    public async Task<bool> InteractWithItemAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        return await item.Logic.OnInteractAsync(ctx, interaction, ct);
    }

    public async Task<bool> ClickItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        await item.Logic.OnClickAsync(ctx, param, ct);

        return true;
    }

    public async Task<bool> SetItemDataAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        string color,
        string text,
        CancellationToken ct
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        if (!await CanEditItemAsync(ctx, item))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        var config = _roomGrain._roomConfig;

        if (!StickieColors.IsValid(color) || text.Length > config.StickieTextMaxLength)
        {
            _roomGrain._logger.LogWarning(
                "Rejected item data on {ItemId} in room {RoomId} from player {PlayerId}: colour {Color}, {Length} characters",
                itemId,
                _roomGrain.RoomId,
                ctx.PlayerId,
                color,
                text.Length
            );

            return false;
        }

        await item.Logic.SetLegacyDataAsync(StickieColors.Compose(color.ToUpperInvariant(), text));

        return true;
    }

    public async Task<bool> SetObjectDataAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        IReadOnlyDictionary<string, string> entries,
        CancellationToken ct
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (!await CanEditItemAsync(ctx, item))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        var config = _roomGrain._roomConfig;

        if (
            entries.Count > config.ObjectDataMaxEntries
            || entries.Any(x =>
                string.IsNullOrEmpty(x.Key)
                || x.Key.Length > config.ObjectDataMaxKeyLength
                || x.Value.Length > config.ObjectDataMaxValueLength
            )
        )
        {
            _roomGrain._logger.LogWarning(
                "Rejected object data on {ItemId} in room {RoomId} from player {PlayerId}: {Count} entries",
                itemId,
                _roomGrain.RoomId,
                ctx.PlayerId,
                entries.Count
            );

            return false;
        }

        return await item.Logic.SetMapDataAsync(entries);
    }

    public async Task<bool> DeleteItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        if (!await CanEditItemAsync(ctx, item))
            throw new TurboException(TurboErrorCodeEnum.NoPermissionToManipulateFurni);

        await _roomGrain.ObjectModule.RemoveObjectAsync(ctx, item, ct, item.OwnerId);

        await _roomGrain
            ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
            .EnqueueDeletedItemAsync(_roomGrain.RoomId, itemId, ct);

        return true;
    }

    /// <summary>The item's owner may always edit it; otherwise room rights are needed.</summary>
    private async Task<bool> CanEditItemAsync(ActionContext ctx, IRoomItem item) =>
        item.OwnerId == ctx.PlayerId
        || await _roomGrain.SecurityModule.CanManipulateFurniAsync(ctx);
}
