using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;

namespace Turbo.Primitives.Grains;

public interface IRoomGrain : IGrainWithIntegerKey
{
    public Task<string> GetWorldTypeAsync();
    public Task<RoomSnapshot> GetSnapshotAsync();
    public Task<RoomMapSnapshot> GetMapSnapshotAsync();
    public Task<IReadOnlyList<RoomFloorItemSnapshot>> GetFloorItemSnapshotsAsync();
}
