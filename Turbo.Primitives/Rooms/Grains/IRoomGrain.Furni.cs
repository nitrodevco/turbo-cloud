using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Rooms.Furniture.Floor;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct);
    public Task<bool> MoveFloorItemByIdAsync(
        long itemId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    );
    public Task<bool> RemoveFloorItemByIdAsync(long itemId, long pickerId, CancellationToken ct);
    public Task<bool> UseFloorItemByIdAsync(
        long itemId,
        int param = -1,
        CancellationToken ct = default
    );
    public Task<bool> ClickFloorItemByIdAsync(
        long itemId,
        int param = -1,
        CancellationToken ct = default
    );
    public Task<bool> ValidateFloorPlacementAsync(
        long itemId,
        int newX,
        int newY,
        Rotation newRotation
    );
    public Task MarkItemAsDirtyAsync(long itemId);
    public Task<RoomFloorItemSnapshot?> GetFloorItemSnapshotByIdAsync(
        long itemId,
        CancellationToken ct
    );
}
