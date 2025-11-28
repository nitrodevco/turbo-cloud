using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct);
    public Task<bool> MoveWallItemByIdAsync(
        ActionContext ctx,
        long itemId,
        string newLocation,
        CancellationToken ct
    );
    public Task<bool> RemoveWallItemByIdAsync(ActionContext ctx, long itemId, CancellationToken ct);
    public Task<bool> UseWallItemByIdAsync(
        ActionContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    );
    public Task<bool> ClickWallItemByIdAsync(
        ActionContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    );
    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        long itemId,
        CancellationToken ct
    );
}
