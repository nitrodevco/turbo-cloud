using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans.Concurrency;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Networking;
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
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Uses up all of <paramref name="itemIds"/> at once, or none (a craft's ingredients): their
    /// rows are deleted in one transaction and they leave the list. Returns what was used up, in
    /// the order asked for, or an empty array when one of them is not held here or has no row.
    /// </summary>
    public Task<ImmutableArray<FurnitureItemSnapshot>> ConsumeAllFurnitureAsync(
        ImmutableArray<RoomObjectId> itemIds,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Takes one sheet off a post-it pad held here, for a note about to be stuck to a wall. A pad
    /// of several sheets keeps one fewer, written to its row, and the client is told how many are
    /// left (<c>PostItPlaced</c>); the last sheet is the pad itself, which is used up under
    /// <paramref name="cause"/> and leaves the list (<c>FurniListRemove</c>). Null when no pad of
    /// that id is held here.
    /// </summary>
    public Task<PostItSheetTakenSnapshot?> TakePostItSheetAsync(
        RoomObjectId padId,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Gives back a sheet <see cref="TakePostItSheetAsync"/> took when no note came of it: the
    /// pad counts it again, or, when the pad went with it or has gone since, a pad of one sheet of
    /// the same kind is made under <paramref name="cause"/>.
    /// </summary>
    public Task ReturnPostItSheetAsync(
        PostItSheetTakenSnapshot taken,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Items whose rows another grain has already taken out of this inventory (put into a wired
    /// chest). Memory only: drops them from the list and tells the client once. Ids not held are
    /// ignored.
    /// </summary>
    public Task ReleaseFurnitureAsync(ImmutableArray<RoomObjectId> itemIds, CancellationToken ct);

    /// <summary>
    /// Gives the player a bought offer's products. Returns the furni it created, teleporter
    /// pairs included; pets, bots and effects are not furni and are not listed. The furni are
    /// numbered from <paramref name="cause"/>'s first seq: the plain ones first, then the pairs.
    /// </summary>
    public Task<ImmutableArray<FurnitureItemSnapshot>> GrantCatalogOfferAsync(
        CatalogOfferSnapshot offer,
        string extraParam,
        int quantity,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Grant an LTD furniture item with serial number to the player's inventory.
    /// </summary>
    /// <param name="catalogProductId">The catalog product ID.</param>
    /// <param name="serialNumber">The unique serial number (e.g., 123).</param>
    /// <param name="seriesSize">The total series size (e.g., 500).</param>
    /// <param name="cause">Why it is granted, for its movement.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task GrantLtdFurnitureAsync(
        int catalogProductId,
        int serialNumber,
        int seriesSize,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Creates one furniture row of a definition in this inventory (a saddle taken off a horse,
    /// a harvested seed). Null when the definition does not exist.
    /// </summary>
    public Task<FurnitureItemSnapshot?> GrantFurnitureAsync(
        int definitionId,
        string? extraDataJson,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Creates one furniture row of a definition in this inventory once per
    /// <paramref name="reference"/>: true when it did, false when this player was granted under
    /// that reference already (or the definition does not exist), and nothing is made.
    /// </summary>
    public Task<bool> GrantFurnitureOnceAsync(
        int definitionId,
        string reference,
        MovementCause cause,
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
        MovementCause cause,
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
    public Task ReceivePresentAsync(
        PresentGrantRequest request,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Wraps a gift from the hotel and gives it to this player, as <see cref="ReceivePresentAsync"/>
    /// does a bought one. Returns the present as listed.
    /// </summary>
    public Task<FurnitureItemSnapshot> ReceiveStaffPresentAsync(
        StaffPresentGrantRequest request,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Takes the item a present holds out of it and lists it here, before the present itself is
    /// deleted. Null when the present holds nothing.
    /// </summary>
    public Task<FurnitureItemSnapshot?> UnwrapPresentAsync(
        RoomObjectId presentId,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Sends <paramref name="sessionKey"/> the player's furni tab, in fragments. From then on that
    /// session's list is kept in step item by item (<c>FurniListAddOrUpdate</c>); a session that
    /// never asked is told nothing, since its client asks for the whole list when it opens.
    /// </summary>
    public Task SendFurnitureInventoryAsync(SessionKey sessionKey, CancellationToken ct);

    public Task<ImmutableArray<FurnitureItemSnapshot>> GetAllItemSnapshotsAsync(
        CancellationToken ct
    );
}
