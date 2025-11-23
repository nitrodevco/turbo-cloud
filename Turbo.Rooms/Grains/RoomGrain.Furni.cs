using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Rooms.Furniture.Floor;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct) =>
        _furniModule.AddFloorItemAsync(item, ct);

    public Task<bool> MoveFloorItemByIdAsync(
        long itemId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    ) => _furniModule.MoveFloorItemByIdAsync(itemId, newX, newY, newRotation, ct);

    public Task<bool> RemoveFloorItemByIdAsync(long itemId, long pickerId, CancellationToken ct) =>
        _furniModule.RemoveFloorItemByIdAsync(itemId, pickerId, ct);

    public Task<bool> UseFloorItemByIdAsync(
        long itemId,
        int param = -1,
        CancellationToken ct = default
    ) => _furniModule.UseFloorItemByIdAsync(itemId, param, ct);

    public Task<bool> ClickFloorItemByIdAsync(
        long itemId,
        int param = -1,
        CancellationToken ct = default
    ) => _furniModule.ClickFloorItemByIdAsync(itemId, param, ct);

    public Task<bool> ValidateFloorPlacementAsync(
        long itemId,
        int newX,
        int newY,
        Rotation newRotation
    ) => _furniModule.ValidateFloorPlacementAsync(itemId, newX, newY, newRotation);

    public Task MarkItemAsDirtyAsync(long itemId) =>
        Task.FromResult(_liveState.DirtyItemIds.Add(itemId));

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
