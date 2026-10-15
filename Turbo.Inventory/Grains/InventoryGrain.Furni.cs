using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Inventory.Grains;

internal sealed partial class InventoryGrain
{
    public Task<ImmutableArray<FurnitureItemSnapshot>> GetAllItemSnapshotsAsync(
        CancellationToken ct
    ) => FurniModule.GetAllAsync(ct);

    public Task<FurnitureItemSnapshot?> GetItemSnapshotAsync(
        RoomObjectId itemId,
        CancellationToken ct
    ) => FurniModule.GetAsync(itemId, ct);

    public Task<ImmutableArray<FurnitureItemSnapshot>> GetItemSnapshotsAsync(
        ImmutableArray<RoomObjectId> itemIds,
        CancellationToken ct
    ) => FurniModule.GetManyAsync(itemIds, ct);

    public async Task<bool> AddFurnitureFromRoomItemSnapshotAsync(
        RoomItemSnapshot snapshot,
        CancellationToken ct
    ) => await FurniModule.AddFromRoomItemsAsync([snapshot], ct) > 0;

    public Task<int> AddFurnitureFromRoomItemSnapshotsAsync(
        ImmutableArray<RoomItemSnapshot> snapshots,
        CancellationToken ct
    ) => FurniModule.AddFromRoomItemsAsync(snapshots, ct);

    public Task<bool> RemoveFurnitureAsync(RoomObjectId itemId, CancellationToken ct) =>
        FurniModule.RemoveAsync(itemId, ct);

    public Task<FurnitureItemSnapshot?> ConsumeFurnitureAsync(
        RoomObjectId itemId,
        CancellationToken ct
    ) => FurniModule.ConsumeAsync(itemId, ct);

    public Task ReleaseFurnitureAsync(ImmutableArray<RoomObjectId> itemIds, CancellationToken ct) =>
        FurniModule.ReleaseAsync(itemIds, ct);

    public Task<FurnitureItemSnapshot?> GrantFurnitureAsync(
        int definitionId,
        string? extraDataJson,
        CancellationToken ct
    ) => FurniModule.GrantAsync(definitionId, extraDataJson, ct);

    public Task GrantLtdFurnitureAsync(
        int catalogProductId,
        int serialNumber,
        int seriesSize,
        CancellationToken ct
    ) => FurniModule.GrantLimitedAsync(catalogProductId, serialNumber, seriesSize, ct);

    public Task<bool> TransferFurnitureAsync(
        ImmutableArray<RoomObjectId> itemIds,
        PlayerId toPlayerId,
        CancellationToken ct
    ) => FurniModule.TransferAsync(itemIds, toPlayerId, ct);

    public Task ReceiveFurnitureAsync(
        ImmutableArray<FurnitureItemSnapshot> items,
        CancellationToken ct
    )
    {
        FurniModule.Receive(items);

        return Task.CompletedTask;
    }

    public Task SendFurnitureInventoryAsync(SessionKey sessionKey, CancellationToken ct) =>
        FurniModule.SendListAsync(sessionKey, ct);
}
