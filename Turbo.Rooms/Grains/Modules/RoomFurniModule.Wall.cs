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
    public async Task<bool> PlaceWallItemAsync(
        ActionContext ctx,
        IRoomWallItem item,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (
            !await _roomGrain.ObjectModule.AttatchObjectAsync(item, ct)
            || !_roomGrain.MapModule.PlaceWallItem(item, x, y, z, rot, wallOffset)
        )
            return false;

        await item.Logic.OnPlaceAsync(ctx, ct);

        item.MarkDirty();

        await _roomGrain.SendComposerToRoomAsync(item.GetAddComposer());

        return true;
    }

    public async Task<bool> MoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        double z,
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

        if (!_roomGrain.MapModule.MoveWallItemItem(wall, x, y, z, rot, wallOffset))
            return false;

        await _roomGrain.SendComposerToRoomAsync(item.GetUpdateComposer());

        await item.Logic.OnMoveAsync(ctx, -1, ct);

        return true;
    }

    public Task<bool> ValidateWallItemPlacementAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot
    ) => Task.FromResult(true);

    public Task<bool> ValidateNewWallItemPlacementAsync(
        ActionContext ctx,
        IRoomWallItem item,
        int x,
        int y,
        double z,
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
