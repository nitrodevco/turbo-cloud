using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Actor;
using Turbo.Primitives.Rooms.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct);
    public Task<bool> MoveWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        string newLocation,
        CancellationToken ct
    );
    public Task<bool> RemoveWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        long pickerId,
        CancellationToken ct
    );
    public Task<bool> UseWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    );
    public Task<bool> ClickWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    );
    public Task<bool> ValidateWallItemPlacementAsync(
        ActorContext ctx,
        long itemId,
        string newLocation
    );
    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        long itemId,
        CancellationToken ct
    );
}
