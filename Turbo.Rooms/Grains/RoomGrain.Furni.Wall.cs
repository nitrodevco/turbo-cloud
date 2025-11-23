using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct) =>
        _furniModule.AddWallItemAsync(item, ct);

    public Task<bool> MoveWallItemByIdAsync(
        long itemId,
        string newLocation,
        CancellationToken ct
    ) => _furniModule.MoveWallItemByIdAsync(itemId, newLocation, ct);

    public Task<bool> RemoveWallItemByIdAsync(long itemId, long pickerId, CancellationToken ct) =>
        _furniModule.RemoveWallItemByIdAsync(itemId, pickerId, ct);

    public Task<bool> UseWallItemByIdAsync(
        long itemId,
        int param = -1,
        CancellationToken ct = default
    ) => _furniModule.UseWallItemByIdAsync(itemId, param, ct);

    public Task<bool> ClickWallItemByIdAsync(
        long itemId,
        int param = -1,
        CancellationToken ct = default
    ) => _furniModule.ClickWallItemByIdAsync(itemId, param, ct);

    public Task<bool> ValidateWallItemPlacementAsync(long itemId, string newLocation) =>
        _furniModule.ValidateWallItemPlacementAsync(itemId, newLocation);

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
