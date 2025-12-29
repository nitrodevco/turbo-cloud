using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Primitives.Rooms.Object.Furniture.Floor;

public interface IRoomFloorItemContext : IRoomItemContext<IRoomFloorItem>
{
    public int GetTileIdx();
    public int GetTileIdx(int x, int y);

    //public IRoomFloorItem Item { get; }
    public void RefreshTile();
    public Task<RoomTileSnapshot> GetTileSnapshotAsync(CancellationToken ct);
}
