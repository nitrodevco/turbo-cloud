using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Actor;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct) =>
        _actionModule.AddFloorItemAsync(item, ct);

    public Task<bool> MoveFloorItemByIdAsync(
        ActorContext ctx,
        long itemId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    ) => _actionModule.MoveFloorItemByIdAsync(ctx, itemId, newX, newY, newRotation, ct);

    public Task<bool> RemoveFloorItemByIdAsync(
        ActorContext ctx,
        long itemId,
        CancellationToken ct
    ) => _actionModule.RemoveFloorItemByIdAsync(ctx, itemId, ct);

    public Task<bool> UseFloorItemByIdAsync(
        ActorContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    ) => _actionModule.UseFloorItemByIdAsync(ctx, itemId, param, ct);

    public Task<bool> ClickFloorItemByIdAsync(
        ActorContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    ) => _actionModule.ClickFloorItemByIdAsync(ctx, itemId, param, ct);

    public Task<bool> ValidateFloorItemPlacementAsync(
        ActorContext ctx,
        long itemId,
        int newX,
        int newY,
        Rotation newRotation
    ) => _furniModule.ValidateFloorItemPlacementAsync(ctx, itemId, newX, newY, newRotation);

    public Task<RoomFloorItemSnapshot?> GetFloorItemSnapshotByIdAsync(
        long itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _liveState.FloorItemsById.TryGetValue(itemId, out var item)
                ? RoomFloorItemSnapshot.FromFloorItem(item)
                : null
        );
}
