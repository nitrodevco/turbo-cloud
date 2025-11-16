using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;

namespace Turbo.Primitives.Orleans.Grains.Room;

public interface IRoomGrain : IGrainWithIntegerKey
{
    public Task EnsureMapBuiltAsync(CancellationToken ct);
    public Task MoveFloorItemAsync(
        long itemId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    );
    public Task<RoomSnapshot> GetSnapshotAsync();
    public Task<RoomSummarySnapshot> GetSummaryAsync();
    public Task<int> GetRoomPopulationAsync();
    public Task<RoomMapSnapshot> GetMapSnapshotAsync();
    public Task<ImmutableArray<RoomFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync();
}
