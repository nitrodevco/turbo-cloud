using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Snapshots.Rooms;
using Turbo.Primitives.Snapshots.Rooms.Mapping;

namespace Turbo.Primitives.Grains;

public interface IRoomGrain : IGrainWithIntegerKey
{
    public ValueTask<string> GetWorldTypeAsync();
    public ValueTask<RoomSnapshot> GetSnapshotAsync();
    public ValueTask<RoomMapSnapshot> GetMapSnapshotAsync();
    public ValueTask<IReadOnlyList<RoomFloorItemSnapshot>> GetFloorItemSnapshotsAsync();
}
