using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Actor;
using Turbo.Primitives.Rooms.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct) =>
        _furniModule.AddWallItemAsync(item, ct);

    public Task<bool> MoveWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        string newLocation,
        CancellationToken ct
    ) => _furniModule.MoveWallItemByIdAsync(ctx, itemId, newLocation, ct);

    public Task<bool> RemoveWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        long pickerId,
        CancellationToken ct
    ) => _furniModule.RemoveWallItemByIdAsync(ctx, itemId, pickerId, ct);

    public Task<bool> UseWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    ) => _furniModule.UseWallItemByIdAsync(ctx, itemId, param, ct);

    public Task<bool> ClickWallItemByIdAsync(
        ActorContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    ) => _furniModule.ClickWallItemByIdAsync(ctx, itemId, param, ct);

    public Task<bool> ValidateWallItemPlacementAsync(
        ActorContext ctx,
        long itemId,
        string newLocation
    ) => _furniModule.ValidateWallItemPlacementAsync(ctx, itemId, newLocation);

    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        long itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _liveState.WallItemsById.TryGetValue(itemId, out var item)
                ? RoomWallItemSnapshot.FromWallItem(item)
                : null
        );
}
