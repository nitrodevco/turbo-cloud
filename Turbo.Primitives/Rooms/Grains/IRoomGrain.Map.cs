using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task ComputeTileAsync(int x, int y);
    public Task ComputeTileAsync(int id);
    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int x, int y, CancellationToken ct);
    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int id, CancellationToken ct);
    public Task<RoomMapSnapshot> GetMapSnapshotAsync(CancellationToken ct);
}
