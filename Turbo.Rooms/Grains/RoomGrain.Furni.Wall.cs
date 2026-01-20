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
        try
        {
            if (!await ActionModule.AddWallItemAsync(item, ct))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
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
        try
        {
            if (!await ActionModule.PlaceWallItemAsync(ctx, item, x, y, z, wallOffset, rot, ct))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
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
        try
        {
            if (
                !await ActionModule.MoveWallItemByIdAsync(
                    ctx,
                    itemId,
                    newX,
                    newY,
                    newZ,
                    wallOffset,
                    newRot,
                    ct
                )
            )
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
    }

    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        RoomObjectId itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.ItemsById.TryGetValue(itemId, out var item)
                ? item is IRoomWallItem wall
                    ? wall.GetSnapshot()
                    : null
                : null
        );

    public Task<ImmutableArray<RoomWallItemSnapshot>> GetAllWallItemSnapshotsAsync(
        CancellationToken ct
    ) => FurniModule.GetAllWallItemSnapshotsAsync(ct);
}
