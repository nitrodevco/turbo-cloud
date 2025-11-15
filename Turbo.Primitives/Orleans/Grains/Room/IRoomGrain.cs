using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Primitives.Orleans.Grains.Room;

public interface IRoomGrain : IGrainWithIntegerKey
{
    public Task<RoomSnapshot> GetSnapshotAsync();
    public Task<RoomSummarySnapshot> GetSummaryAsync();
}
