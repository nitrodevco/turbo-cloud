using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct) =>
        _actionModule.AddFloorItemAsync(item, ct);

    public Task<bool> MoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    ) => _actionModule.MoveFloorItemByIdAsync(ctx, objectId, newX, newY, newRotation, ct);

    public Task<bool> RemoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct
    ) => _actionModule.RemoveFloorItemByIdAsync(ctx, objectId, ct);

    public Task<bool> UseFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct,
        int param = -1
    ) => _actionModule.UseFloorItemByIdAsync(ctx, objectId, ct, param);

    public Task<bool> ClickFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct,
        int param = -1
    ) => _actionModule.ClickFloorItemByIdAsync(ctx, objectId, ct, param);

    public Task<RoomFloorItemSnapshot?> GetFloorItemSnapshotByIdAsync(
        RoomObjectId objectId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _liveState.FloorItemsById.TryGetValue(objectId.Value, out var item)
                ? item.GetSnapshot()
                : null
        );

    public Task<ImmutableArray<RoomFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync(
        CancellationToken ct
    ) => _furniModule.GetAllFloorItemSnapshotsAsync(ct);
}
