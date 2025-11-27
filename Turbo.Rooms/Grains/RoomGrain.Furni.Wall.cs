using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Actor;
using Turbo.Primitives.Rooms.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct) =>
        _actionModule.AddWallItemAsync(item, ct);

    public Task<bool> MoveWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        string newLocation,
        CancellationToken ct
    ) => _actionModule.MoveWallItemByIdAsync(ctx, itemId, newLocation, ct);

    public Task<bool> RemoveWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        CancellationToken ct
    ) => _actionModule.RemoveWallItemByIdAsync(ctx, itemId, ct);

    public Task<bool> UseWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    ) => _actionModule.UseWallItemByIdAsync(ctx, itemId, param, ct);

    public Task<bool> ClickWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    ) => _actionModule.ClickWallItemByIdAsync(ctx, itemId, param, ct);

    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        long itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _liveState.WallItemsById.TryGetValue(itemId, out var item) ? item.GetSnapshot() : null
        );
}
