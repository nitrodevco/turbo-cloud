using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct)
    {
        try
        {
            if (!await _actionModule.AddFloorItemAsync(item, ct))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
    }

    public async Task<bool> MoveFloorItemByIdAsync(
        ActionContext ctx,
        int itemId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    )
    {
        try
        {
            if (
                !await _actionModule.MoveFloorItemByIdAsync(
                    ctx,
                    itemId,
                    newX,
                    newY,
                    newRotation,
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

    public async Task<bool> RemoveFloorItemByIdAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct
    )
    {
        try
        {
            if (!await _actionModule.RemoveFloorItemByIdAsync(ctx, itemId, ct))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
    }

    public async Task<bool> UseFloorItemByIdAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        try
        {
            if (!await _actionModule.UseFloorItemByIdAsync(ctx, itemId, ct, param))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
    }

    public async Task<bool> ClickFloorItemByIdAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        try
        {
            if (!await _actionModule.ClickFloorItemByIdAsync(ctx, itemId, ct, param))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
    }

    public Task<RoomFloorItemSnapshot?> GetFloorItemSnapshotByIdAsync(
        int itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _liveState.FloorItemsById.TryGetValue(itemId, out var item) ? item.GetSnapshot() : null
        );

    public Task<ImmutableArray<RoomFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync(
        CancellationToken ct
    ) => _furniModule.GetAllFloorItemSnapshotsAsync(ct);
}
