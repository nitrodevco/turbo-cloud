using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct);
    public Task<bool> MoveWallItemByIdAsync(long itemId, string newLocation, CancellationToken ct);
    public Task<bool> RemoveWallItemByIdAsync(long itemId, long pickerId, CancellationToken ct);
    public Task<bool> UseWallItemByIdAsync(
        long itemId,
        int param = -1,
        CancellationToken ct = default
    );
    public Task<bool> ClickWallItemByIdAsync(
        long itemId,
        int param = -1,
        CancellationToken ct = default
    );
    public Task<bool> ValidateWallItemPlacementAsync(long itemId, string newLocation);
    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        long itemId,
        CancellationToken ct
    );
}
