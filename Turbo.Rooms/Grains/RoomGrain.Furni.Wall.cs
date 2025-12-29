using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct)
    {
        return await _actionModule.AddWallItemAsync(item, ct);
    }

    public async Task<bool> PlaceWallItemAsync(
        ActionContext ctx,
        FurnitureItemSnapshot item,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    )
    {
        return await _actionModule.PlaceWallItemAsync(ctx, item, x, y, z, wallOffset, rot, ct);
    }

    public async Task<bool> MoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int newX,
        int newY,
        double newZ,
        int wallOffset,
        Rotation newRot,
        CancellationToken ct
    )
    {
        return await _actionModule.MoveWallItemByIdAsync(
            ctx,
            itemId,
            newX,
            newY,
            newZ,
            wallOffset,
            newRot,
            ct
        );
    }

    public async Task<bool> RemoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    )
    {
        return await _actionModule.RemoveWallItemByIdAsync(ctx, itemId, ct);
    }

    public async Task<bool> UseWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        return await _actionModule.UseWallItemByIdAsync(ctx, itemId, ct, param);
    }

    public async Task<bool> ClickWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        return await _actionModule.ClickWallItemByIdAsync(ctx, itemId, ct, param);
    }

    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        RoomObjectId itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _liveState.WallItemsById.TryGetValue(itemId, out var item) ? item.GetSnapshot() : null
        );

    public Task<ImmutableArray<RoomWallItemSnapshot>> GetAllWallItemSnapshotsAsync(
        CancellationToken ct
    ) => _furniModule.GetAllWallItemSnapshotsAsync(ct);
}
