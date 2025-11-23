using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Furniture.Floor;

public interface IRoomFloorItemContext : IRoomItemContext
{
    public IRoomFloorItem Item { get; }
    public Task MarkItemDirtyAsync();
    public Task AddItemAsync(CancellationToken ct);
    public Task UpdateItemAsync(CancellationToken ct);
    public Task RefreshStuffDataAsync(CancellationToken ct);
    public Task RemoveItemAsync(
        long pickerId,
        bool isExpired = false,
        int delay = 0,
        CancellationToken ct = default
    );
    public Task RefreshTileAsync();
    public Task<RoomTileSnapshot> GetTileSnapshotAsync(CancellationToken ct);
}
