using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct);
    public Task<bool> MoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        string newLocation,
        CancellationToken ct
    );
    public Task<bool> RemoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct
    );
    public Task<bool> UseWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int param = -1,
        CancellationToken ct = default
    );
    public Task<bool> ClickWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int param = -1,
        CancellationToken ct = default
    );
    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        RoomObjectId objectId,
        CancellationToken ct
    );
}
