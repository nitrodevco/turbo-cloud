using System.Collections.Immutable;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;

namespace Turbo.Primitives.Orleans.Grains;

public interface IRoomMapGrain : IGrainWithIntegerKey
{
    public Task<string> GetWorldTypeAsync();
    public Task<RoomMapSnapshot> GetMapSnapshotAsync();
    public Task<ImmutableArray<RoomFloorItemSnapshot>> GetFloorItemSnapshotsAsync();
}
