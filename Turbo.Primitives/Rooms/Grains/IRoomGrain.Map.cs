using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task ComputeTileAsync(int x, int y);
    public Task ComputeTileAsync(int id);
    public Task<RoomMapSnapshot> GetMapSnapshotAsync(CancellationToken ct);
}
