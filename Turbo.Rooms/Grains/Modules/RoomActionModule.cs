using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;

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

        var pickupType = await _roomGrain.SecurityModule.GetFurniPickupTypeAsync(ctx);

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

    public async Task<bool> UseItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(itemId, out var item))
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        var usagePolicy = item.Logic.GetUsagePolicy();

        if (!await _roomGrain.SecurityModule.CanUseFurniAsync(ctx, usagePolicy))
            return false;

        await item.Logic.OnUseAsync(ctx, param, ct);

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
