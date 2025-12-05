using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public void ComputeTile(int x, int y) => _mapModule.ComputeTile(x, y);

    public void ComputeTile(int id) => _mapModule.ComputeTile(id);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int x, int y, CancellationToken ct) =>
        _mapModule.GetTileSnapshotAsync(x, y, ct);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int id, CancellationToken ct) =>
        _mapModule.GetTileSnapshotAsync(id, ct);

    public Task<RoomMapSnapshot> GetMapSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(_mapModule.GetMapSnapshot(ct));
}
