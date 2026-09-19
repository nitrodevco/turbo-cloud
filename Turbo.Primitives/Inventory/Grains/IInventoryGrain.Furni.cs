using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Inventory.Grains;

public partial interface IInventoryGrain
{
    public Task<bool> AddFurnitureFromRoomItemSnapshotAsync(
        RoomItemSnapshot snapshot,
        CancellationToken ct
    );

    /// <summary>
    /// Several picked-up items at once (a room being deleted): one call per owner, one
    /// inventory refresh. Returns how many were listed.
    /// </summary>
    public Task<int> AddFurnitureFromRoomItemSnapshotsAsync(
        ImmutableArray<RoomItemSnapshot> snapshots,
        CancellationToken ct
    );
    public Task<bool> RemoveFurnitureAsync(RoomObjectId itemId, CancellationToken ct);
    public Task GrantCatalogOfferAsync(
        CatalogOfferSnapshot offer,
        string extraParam,
        int quantity,
        CancellationToken ct
    );

    /// <summary>
    /// Grant an LTD furniture item with serial number to the player's inventory.
    /// </summary>
    /// <param name="catalogProductId">The catalog product ID.</param>
    /// <param name="serialNumber">The unique serial number (e.g., 123).</param>
    /// <param name="seriesSize">The total series size (e.g., 500).</param>
    /// <param name="ct">Cancellation token.</param>
    public Task GrantLtdFurnitureAsync(
        int catalogProductId,
        int serialNumber,
        int seriesSize,
        CancellationToken ct
    );

    /// <summary>
    /// Creates one furniture row of a definition in this inventory (a saddle taken off a horse,
    /// a harvested seed). Null when the definition does not exist.
    /// </summary>
    public Task<FurnitureItemSnapshot?> GrantFurnitureAsync(
        int definitionId,
        string? extraDataJson,
        CancellationToken ct
    );

    public Task<FurnitureItemSnapshot?> GetItemSnapshotAsync(
        RoomObjectId itemId,
        CancellationToken ct
    );

    /// <summary>The items of these ids held here; ids not held are left out.</summary>
    public Task<ImmutableArray<FurnitureItemSnapshot>> GetItemSnapshotsAsync(
        ImmutableArray<RoomObjectId> itemIds,
        CancellationToken ct
    );

    /// <summary>
    /// Moves items held here to another player (a completed trade). All or nothing: the rows
    /// change owner in one statement and the receiving inventory is told afterwards. False
    /// when any item is no longer here.
    /// </summary>
    public Task<bool> TransferFurnitureAsync(
        ImmutableArray<RoomObjectId> itemIds,
        PlayerId toPlayerId,
        CancellationToken ct
    );

    /// <summary>Items whose rows were just re-owned to this player by another inventory.</summary>
    public Task ReceiveFurnitureAsync(
        ImmutableArray<FurnitureItemSnapshot> items,
        CancellationToken ct
    );
    public Task<ImmutableArray<FurnitureItemSnapshot>> GetAllItemSnapshotsAsync(
        CancellationToken ct
    );
}
