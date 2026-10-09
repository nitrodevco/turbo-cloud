using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans.Concurrency;
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

    /// <summary>
    /// Uses an item up: its row is deleted and it leaves the list (a room paper applied to a
    /// room). Returns what was used up, or null when the item is not held here.
    /// </summary>
    public Task<FurnitureItemSnapshot?> ConsumeFurnitureAsync(
        RoomObjectId itemId,
        CancellationToken ct
    );

    /// <summary>
    /// Items whose rows another grain has already taken out of this inventory (put into a wired
    /// chest). Memory only: drops them from the list and tells the client once. Ids not held are
    /// ignored.
    /// </summary>
    public Task ReleaseFurnitureAsync(ImmutableArray<RoomObjectId> itemIds, CancellationToken ct);
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

    /// <summary>
    /// Items whose rows were just re-owned to this player by another inventory. An interleaved,
    /// memory-only tell: one inventory awaits another here, and two trades moving items in
    /// opposite directions at once would otherwise leave both waiting on each other. A section
    /// that is not loaded only tells the client; the rows are read when it loads.
    /// </summary>
    [AlwaysInterleave]
    public Task ReceiveFurnitureAsync(
        ImmutableArray<FurnitureItemSnapshot> items,
        CancellationToken ct
    );

    /// <summary>
    /// Wraps a bought gift and gives it to this player: the present is listed, and the item
    /// inside is held by it, out of the list, until it is opened.
    /// </summary>
    public Task ReceivePresentAsync(PresentGrantRequest request, CancellationToken ct);

    /// <summary>
    /// Wraps a gift from the hotel and gives it to this player, as <see cref="ReceivePresentAsync"/>
    /// does a bought one. Returns the present as listed.
    /// </summary>
    public Task<FurnitureItemSnapshot> ReceiveStaffPresentAsync(
        StaffPresentGrantRequest request,
        CancellationToken ct
    );

    /// <summary>
    /// Takes the item a present holds out of it and lists it here, before the present itself is
    /// deleted. Null when the present holds nothing.
    /// </summary>
    public Task<FurnitureItemSnapshot?> UnwrapPresentAsync(
        RoomObjectId presentId,
        CancellationToken ct
    );

    /// <summary>Sends the player their furni tab, in fragments.</summary>
    public Task SendFurnitureInventoryAsync(CancellationToken ct);

    public Task<ImmutableArray<FurnitureItemSnapshot>> GetAllItemSnapshotsAsync(
        CancellationToken ct
    );
}
