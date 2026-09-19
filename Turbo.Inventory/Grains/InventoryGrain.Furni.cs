using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Inventory.Grains;

internal sealed partial class InventoryGrain
{
    public Task<ImmutableArray<FurnitureItemSnapshot>> GetAllItemSnapshotsAsync(
        CancellationToken ct
    ) => _furniModule.GetAllAsync(ct);

    public Task<FurnitureItemSnapshot?> GetItemSnapshotAsync(
        RoomObjectId itemId,
        CancellationToken ct
    ) => _furniModule.GetAsync(itemId, ct);

    public Task<ImmutableArray<FurnitureItemSnapshot>> GetItemSnapshotsAsync(
        ImmutableArray<RoomObjectId> itemIds,
        CancellationToken ct
    ) => _furniModule.GetManyAsync(itemIds, ct);

    public async Task<bool> AddFurnitureFromRoomItemSnapshotAsync(
        RoomItemSnapshot snapshot,
        CancellationToken ct
    ) => await _furniModule.AddFromRoomItemsAsync([snapshot], ct) > 0;

    public Task<int> AddFurnitureFromRoomItemSnapshotsAsync(
        ImmutableArray<RoomItemSnapshot> snapshots,
        CancellationToken ct
    ) => _furniModule.AddFromRoomItemsAsync(snapshots, ct);

    public Task<bool> RemoveFurnitureAsync(RoomObjectId itemId, CancellationToken ct) =>
        _furniModule.RemoveAsync(itemId, ct);

    public Task<FurnitureItemSnapshot?> GrantFurnitureAsync(
        int definitionId,
        string? extraDataJson,
        CancellationToken ct
    ) => _furniModule.GrantAsync(definitionId, extraDataJson, ct);

    public Task GrantLtdFurnitureAsync(
        int catalogProductId,
        int serialNumber,
        int seriesSize,
        CancellationToken ct
    ) => _furniModule.GrantLimitedAsync(catalogProductId, serialNumber, seriesSize, ct);

    public Task<bool> TransferFurnitureAsync(
        ImmutableArray<RoomObjectId> itemIds,
        PlayerId toPlayerId,
        CancellationToken ct
    ) => _furniModule.TransferAsync(itemIds, toPlayerId, ct);

    public Task ReceiveFurnitureAsync(
        ImmutableArray<FurnitureItemSnapshot> items,
        CancellationToken ct
    ) => _furniModule.ReceiveAsync(items, ct);
}
