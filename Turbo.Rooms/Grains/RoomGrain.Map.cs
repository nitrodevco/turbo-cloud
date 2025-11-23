using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task ComputeTileAsync(int x, int y) => _mapModule.ComputeTileAsync(x, y);

    public Task ComputeTileAsync(int id) => _mapModule.ComputeTileAsync(id);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int x, int y, CancellationToken ct) =>
        _mapModule.GetTileSnapshotAsync(x, y, ct);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int id, CancellationToken ct) =>
        _mapModule.GetTileSnapshotAsync(id, ct);

    public Task<RoomMapSnapshot> GetMapSnapshotAsync(CancellationToken ct) =>
        _mapModule.GetMapSnapshotAsync(ct);
}
