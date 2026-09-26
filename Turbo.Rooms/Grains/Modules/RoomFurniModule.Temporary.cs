using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots;
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
/// no persistence. What keeps them apart is their id, which is negative
/// (<see cref="IRoomItem.IsTemporary"/>); every packet handler refuses ids that are not
/// positive, so a player can neither pick one up nor move it.
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
        if (TemporaryItemCount >= _roomGrain._roomConfig.TemporaryFurniMax)
        {
            _roomGrain._logger.LogDebug(
                "Room {RoomId} holds its {Max} temporary furni; none was added",
                _roomGrain.RoomId,
                _roomGrain._roomConfig.TemporaryFurniMax
            );

            return null;
        }

        var item = _roomGrain._itemsLoader.CreateFloorItem(
            _roomGrain._state.NextTemporaryItemId,
            ownerId,
            definition
        );

        if (!await PlaceFloorItemAsync(ctx, item, x, y, rot, ct, z))
            return null;

        // Only an id that was used is spent.
        _roomGrain._state.NextTemporaryItemId--;

        return item;
    }

    /// <summary>Takes a temporary furni out of the room. False for a furni that is not one.</summary>
    public Task<bool> RemoveTemporaryItemAsync(
        ActionContext ctx,
        IRoomItem item,
        CancellationToken ct
    ) =>
        item.IsTemporary
            ? _roomGrain.ObjectModule.RemoveObjectAsync(ctx, item, ct)
            : Task.FromResult(false);
}
