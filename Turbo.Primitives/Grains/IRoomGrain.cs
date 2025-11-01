using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Snapshots.Rooms;

namespace Turbo.Primitives.Grains;

public interface IRoomGrain : IGrainWithIntegerKey
{
    public ValueTask<RoomSnapshot> GetSnapshotAsync(CancellationToken ct);
}
