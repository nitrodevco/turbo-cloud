using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomFurniModule
{
    /// <summary>
    /// The one way a new floor item comes into the room, whoever brings it: a player from their
    /// inventory, a Builders Club borrow, a temporary furni, a present's contents. It checks the
    /// spot (<see cref="CanPlaceFloorItem(IRoomFloorItem, int, int, Rotation)"/>) and the room's
    /// placement limits here, so no caller can forget either. A spot that does not take the item
    /// is false; a limit that refuses throws, as <see cref="IRoomPlacementLimit"/> says.
    /// </summary>
    public async Task<bool> PlaceFloorItemAsync(
        ActionContext ctx,
        IRoomFloorItem item,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct,
        Altitude? z = null
    )
    {
        if (!CanPlaceFloorItem(item, x, y, rot))
            return false;

        // A limit tells its own kind of furni by the logic, which a new item does not have yet.
        _roomGrain.ObjectModule.EnsureLogic(item);
        EnsureWithinPlacementLimits(item);

        // The item stands where it goes before it is attached, so its logic and the map see
        // it there from the start rather than at the origin tile it was created on.
        item.SetPosition(x, y);
        item.SetPositionZ(
            z ?? _roomGrain.MapModule.GetTileHeight(_roomGrain.MapModule.ToIdx(x, y))
        );
        item.SetRotation(rot);

        if (!await _roomGrain.ObjectModule.AttatchObjectAsync(item, ct))
            return false;

        await item.Logic.OnPlaceAsync(ctx, ct);

        item.MarkDirty();

        await _roomGrain.SendComposerToRoomAsync(item.GetAddComposer(), ct);

        return true;
    }

    public async Task<bool> MoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Altitude? z,
        Rotation? rot,
        CancellationToken ct
    )
    {
        if (
            !_roomGrain._state.ItemsById.TryGetValue(itemId, out var item)
            || item is not IRoomFloorItem floor
        )
            throw new TurboException(TurboErrorCodeEnum.FloorItemNotFound);

        return await MoveFloorItemAsync(
            ctx,
            floor,
            _roomGrain.MapModule.ToIdx(x, y),
            z,
            rot,
            announce: true,
            ct
        );
    }

    /// <summary>
    /// The one way a floor item changes tile, whoever moves it: the map is updated and the
    /// item's logic hears of it (a roller re-registers its tile, a wired box its stack). A
    /// caller that tells the room itself passes <paramref name="announce"/> false; wired does,
    /// because it sends all the moves of one action as a single animated packet.
    /// </summary>
    public async Task<bool> MoveFloorItemAsync(
        ActionContext ctx,
        IRoomFloorItem item,
        int tileIdx,
        Altitude? z,
        Rotation? rot,
        bool announce,
        CancellationToken ct
    )
    {
        var prevIdx = _roomGrain.MapModule.ToIdx(item.X, item.Y);

        if (!_roomGrain.MapModule.MoveFloorItem(item, tileIdx, z, rot))
            return false;

        if (announce)
            await _roomGrain.SendComposerToRoomAsync(item.GetUpdateComposer(), ct);

        await item.Logic.OnMoveAsync(ctx, prevIdx, ct);

        return true;
    }

    /// <summary>
    /// Whether an item already in the room may stand at a tile and rotation; false for an item
    /// that is not. Pure room state, so synchronous callers (wired conditions) use it directly.
    /// </summary>
    public bool CanPlaceFloorItem(RoomObjectId itemId, int x, int y, Rotation rot) =>
        TryGetFloorItem(itemId, out var item) && CanPlaceFloorItem(item, x, y, rot);

    /// <summary>
    /// Whether a floor item, new or already in the room, may stand at a tile and rotation. The
    /// one occupancy check: moving, placing and every wired mover ask here. Bounds-safe: a
    /// footprint that leaves the map is simply a no, so callers do not check bounds first.
    /// </summary>
    public bool CanPlaceFloorItem(IRoomFloorItem item, int x, int y, Rotation rot)
    {
        if (
            !_roomGrain.MapModule.InBounds(x, y)
            || !_roomGrain.MapModule.GetTileIdForSize(
                x,
                y,
                rot,
                item.Definition.Width,
                item.Definition.Length,
                out var tileIds
            )
        )
            return false;

        var stackHeight = item.GetStackHeight();

        foreach (var idx in tileIds)
        {
            var tileFlags = _roomGrain._state.TileFlags[idx];
            var tileHeight = _roomGrain._state.TileHeights[idx];

            _roomGrain.MapModule.TryGetHighestFloorItem(idx, out var top);

            // An item already standing here is moved or turned in place: it is not in its own way.
            var isSelf = ReferenceEquals(top, item);

            if (isSelf)
                tileHeight -= stackHeight;

            if (
                tileFlags.Has(RoomTileFlags.Disabled)
                || tileHeight + stackHeight > _roomGrain._roomConfig.MaxStackHeight
                || (tileFlags.Has(RoomTileFlags.StackBlocked) && !isSelf)
            )
                return false;

            if (tileFlags.Has(RoomTileFlags.AvatarOccupied))
            {
                // Turning an item in place never lands it on anyone new.
                var turningInPlace = isSelf && item.Rotation != rot;

                if (!_roomGrain._roomConfig.PlaceItemsOnAvatars && !turningInPlace)
                    return false;

                // The avatars there end up on the item being placed, so it is that item which
                // has to hold them, not whatever they stand on now. The move check used to ask
                // the tile and the new-item check the item, so the same spot could be refused
                // for one and allowed for the other.
                if (!CanHoldAvatar(item))
                    return false;
            }

            if (
                !isSelf
                && top?.Logic is FurnitureRollerLogic
                && (
                    item.Definition.Width > 1
                    || item.Definition.Length > 1
                    || item.Logic is FurnitureRollerLogic
                )
            )
                return false;
        }

        return true;
    }

    /// <summary>
    /// Whether an avatar can stay on top of this item. A new item may not have its logic yet;
    /// its definition says as much then.
    /// </summary>
    private static bool CanHoldAvatar(IRoomFloorItem item) =>
        item.Logic is { } logic
            ? logic.CanWalk() || logic.CanSit() || logic.CanLay()
            : item.Definition.CanWalk || item.Definition.CanSit || item.Definition.CanLay;

    public Task<ImmutableArray<RoomFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync(
        CancellationToken ct
    ) =>
        Task.FromResult(
            _roomGrain
                ._state.ItemsById.Values.OfType<IRoomFloorItem>()
                .Select(x => x.GetSnapshot())
                .ToImmutableArray()
        );

    public bool GetTileIdForFloorItem(IRoomFloorItem item, out List<int> tileIds) =>
        _roomGrain.MapModule.GetTileIdForSize(
            item.X,
            item.Y,
            item.Rotation,
            item.Definition.Width,
            item.Definition.Length,
            out tileIds
        );
}
