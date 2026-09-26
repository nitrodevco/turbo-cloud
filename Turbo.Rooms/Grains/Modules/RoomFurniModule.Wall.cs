using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomFurniModule
{
    /// <summary>
    /// The wall counterpart of <see cref="PlaceFloorItemAsync"/>: every new wall item comes in
    /// here, and the spot and the room's placement limits are checked here.
    /// </summary>
    public async Task<bool> PlaceWallItemAsync(
        ActionContext ctx,
        IRoomWallItem item,
        int x,
        int y,
        Altitude z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (!await ValidateNewWallItemPlacementAsync(ctx, item, x, y, z, wallOffset, rot))
            return false;

        // A limit tells its own kind of furni by the logic, which a new item does not have yet.
        _roomGrain.ObjectModule.EnsureLogic(item);
        EnsureWithinPlacementLimits(item);

        // Positioned before it is attached, as a floor item is.
        item.SetPosition(x, y);
        item.SetPositionZ(z);
        item.SetRotation(rot);
        item.SetWallOffset(wallOffset);

        if (!await _roomGrain.ObjectModule.AttatchObjectAsync(item, ct))
            return false;

        await item.Logic.OnPlaceAsync(ctx, ct);

        item.MarkDirty();

        await _roomGrain.SendComposerToRoomAsync(item.GetAddComposer(), ct);

        return true;
    }

    public async Task<bool> MoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Altitude z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (
            !_roomGrain._state.ItemsById.TryGetValue(itemId, out var item)
            || item is not IRoomWallItem wall
        )
            throw new TurboException(TurboErrorCodeEnum.WallItemNotFound);

        return await MoveWallItemAsync(ctx, wall, x, y, z, wallOffset, rot, announce: true, ct);
    }

    /// <summary>The wall counterpart of <see cref="MoveFloorItemAsync"/>.</summary>
    public async Task<bool> MoveWallItemAsync(
        ActionContext ctx,
        IRoomWallItem item,
        int x,
        int y,
        Altitude z,
        int wallOffset,
        Rotation rot,
        bool announce,
        CancellationToken ct
    )
    {
        if (!_roomGrain.MapModule.MoveWallItem(item, x, y, z, rot, wallOffset))
            return false;

        if (announce)
            await _roomGrain.SendComposerToRoomAsync(item.GetUpdateComposer(), ct);

        await item.Logic.OnMoveAsync(ctx, -1, ct);

        return true;
    }

    public Task<bool> ValidateWallItemPlacementAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Altitude z,
        int wallOffset,
        Rotation rot
    ) => Task.FromResult(true);

    private Task<bool> ValidateNewWallItemPlacementAsync(
        ActionContext ctx,
        IRoomWallItem item,
        int x,
        int y,
        Altitude z,
        int wallOffset,
        Rotation rot
    ) => Task.FromResult(true);

    public Task<ImmutableArray<RoomWallItemSnapshot>> GetAllWallItemSnapshotsAsync(
        CancellationToken ct
    ) =>
        Task.FromResult(
            _roomGrain
                ._state.ItemsById.Values.OfType<IRoomWallItem>()
                .Select(x => x.GetSnapshot())
                .ToImmutableArray()
        );
}
