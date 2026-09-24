using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<bool> AddItemAsync(IRoomItem item, CancellationToken ct);
    public Task<bool> RemoveItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    );
    public Task<bool> UseItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    );
    public Task<bool> ClickItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    );

    /// <summary>
    /// A dedicated furniture action such as throwing a dice. False when the item does not
    /// respond to it, the caller may not use the item, or the request is invalid right now.
    /// </summary>
    public Task<bool> InteractWithItemAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        FurnitureInteraction interaction,
        CancellationToken ct
    );

    /// <summary>Sets a post-it's colour and text. Rights or ownership of the note required.</summary>
    public Task<bool> SetItemDataAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        string color,
        string text,
        CancellationToken ct
    );

    /// <summary>Merges key/value entries into an item's map data. Rights required.</summary>
    public Task<bool> SetObjectDataAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        IReadOnlyDictionary<string, string> entries,
        CancellationToken ct
    );

    /// <summary>The item's legacy data string, or null when it does not exist.</summary>
    public Task<string?> GetItemDataAsync(RoomObjectId itemId, CancellationToken ct);

    /// <summary>
    /// Destroys an item (a post-it or photo): it leaves the room and is deleted rather than
    /// returned to anyone's inventory. Rights or ownership required.
    /// </summary>
    public Task<bool> DeleteItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    );

    public Task<ImmutableDictionary<PlayerId, string>> GetAllOwnersAsync(CancellationToken ct);

    /// <summary>
    /// How many items this player has standing in this room. Counted from the live room rather
    /// than from the furniture rows, because an item placed a moment ago has not been flushed
    /// yet and the player is about to be told how much they stand to get back.
    /// </summary>
    public Task<int> GetItemCountByOwnerAsync(PlayerId ownerId, CancellationToken ct);
    public Task<RoomItemSnapshot?> GetItemSnapshotByIdAsync(
        RoomObjectId itemId,
        CancellationToken ct
    );
}
