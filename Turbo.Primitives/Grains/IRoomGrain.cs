using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Snapshots.Rooms;
using Turbo.Primitives.Snapshots.Rooms.Mapping;

namespace Turbo.Primitives.Grains;

public interface IRoomGrain : IGrainWithIntegerKey
{
    public ValueTask<RoomSnapshot> GetSnapshotAsync();
    public ValueTask<RoomMapSnapshot> GetMapSnapshotAsync();
}
