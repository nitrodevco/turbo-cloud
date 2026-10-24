using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// Temporary furni: floor items the room makes up from a definition and that last until they
/// are removed or the room unloads. They are in the room like any furni (on the map, in the
/// furni list a player is sent, open to wired) and nowhere else: no row, no owner's inventory,
/// no persistence. What keeps them apart is their id, in the client's temporary band
/// (<see cref="FurniIdBands"/>, <see cref="IRoomItem.IsTemporary"/>): the client draws their info
/// stand as temporary furni. Whoever may handle furni in the room moves, uses and picks one up
/// as any other (official client capture, WIN63-202610131318); picked up, it is simply gone.
/// </summary>
public sealed partial class RoomFurniModule
{
    public int TemporaryItemCount => Items.Count(x => x.IsTemporary);

    /// <summary>
    /// Places a temporary furni, on top of the tile or at <paramref name="z"/>. Null when the
    /// room holds its share of them already or the furni does not fit there; neither is an
    /// error, a stack that places furni runs into both all the time.
    /// </summary>
    public async Task<IRoomFloorItem?> PlaceTemporaryFloorItemAsync(
        ActionContext ctx,
        FurnitureDefinitionSnapshot definition,
        PlayerId ownerId,
        int x,
        int y,
        Altitude? z,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (TemporaryItemCount >= RoomConfig.TemporaryFurniMax)
        {
            Logger.LogDebug(
                "Room {RoomId} holds its {Max} temporary furni; none was added",
                _roomGrain.RoomId,
                RoomConfig.TemporaryFurniMax
            );

            return null;
        }

        if (NextTemporaryItemId() is not { } objectId)
            return null;

        var item = _roomGrain._itemsLoader.CreateFloorItem(objectId, ownerId, definition);

        if (!await PlaceFloorItemAsync(ctx, item, x, y, rot, ct, z))
            return null;

        return item;
    }

    /// <summary>
    /// The lowest free id of the temporary band. A removed temporary furni's id is handed out
    /// again, as a borrowed one's is, and its stored wired values leave with it
    /// (<c>RoomWiredSystem.ForgetStoredValuesOfUnownedFurni</c>). Null when the band is full,
    /// which <c>RoomConfig.TemporaryFurniMax</c> should reach first.
    /// </summary>
    private RoomObjectId? NextTemporaryItemId()
    {
        var used = Items.Where(x => x.IsTemporary).Select(x => x.ObjectId.Value).ToHashSet();

        for (
            var offset = 0;
            offset <= used.Count && offset < FurniIdBands.TemporaryCapacity;
            offset++
        )
        {
            var objectId = FurniIdBands.TemporaryMin + offset;

            if (!used.Contains(objectId))
                return (RoomObjectId)objectId;
        }

        return null;
    }

    /// <summary>Takes a temporary furni out of the room. False for a furni that is not one.</summary>
    public Task<bool> RemoveTemporaryItemAsync(
        ActionContext ctx,
        IRoomItem item,
        CancellationToken ct
    ) => item.IsTemporary ? ObjectModule.RemoveObjectAsync(ctx, item, ct) : Task.FromResult(false);

    /// <summary>
    /// Takes the temporary furni among <paramref name="items"/> out of the room and tells the
    /// room once: one ObjectRemoveMultiple with every id in the given order and picker -1, as
    /// Habbo's Remove Temporary Furni does (official client capture 2026-10-24, four copies). The
    /// others stay. False when none was temporary.
    /// </summary>
    public async Task<bool> RemoveTemporaryItemsAsync(
        ActionContext ctx,
        IEnumerable<IRoomFloorItem> items,
        CancellationToken ct
    )
    {
        var removed = new List<long>();

        foreach (var item in items)
        {
            if (
                item.IsTemporary
                && await ObjectModule.RemoveObjectAsync(ctx, item, ct, announce: false)
            )
                removed.Add(item.ObjectId.Value);
        }

        if (removed.Count == 0)
            return false;

        await _roomGrain.SendComposerToRoomAsync(
            new ObjectRemoveMultipleMessageComposer
            {
                ObjectIdsToRemove = [.. removed],
                PickerId = PlayerId.Invalid,
                ByInvisibleFurniManager = false,
            },
            ct
        );

        return true;
    }
}
