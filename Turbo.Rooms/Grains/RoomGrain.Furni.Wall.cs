using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct) =>
        _actionModule.AddWallItemAsync(item, ct);

    public Task<bool> MoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        string newLocation,
        CancellationToken ct
    ) => _actionModule.MoveWallItemByIdAsync(ctx, objectId, newLocation, ct);

    public Task<bool> RemoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct
    ) => _actionModule.RemoveWallItemByIdAsync(ctx, objectId, ct);

    public Task<bool> UseWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct,
        int param = -1
    ) => _actionModule.UseWallItemByIdAsync(ctx, objectId, ct, param);

    public Task<bool> ClickWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct,
        int param = -1
    ) => _actionModule.ClickWallItemByIdAsync(ctx, objectId, ct, param);

    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        RoomObjectId objectId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _liveState.WallItemsById.TryGetValue(objectId.Value, out var item)
                ? item.GetSnapshot()
                : null
        );
}
