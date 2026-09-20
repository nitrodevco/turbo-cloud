using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;

namespace Turbo.Primitives.Rooms.Providers;

public interface IRoomItemsProvider
{
    public Task<(
        IReadOnlyList<IRoomFloorItem>,
        IReadOnlyList<IRoomWallItem>,
        IReadOnlyDictionary<PlayerId, string>
    )> LoadByRoomIdAsync(RoomId roomId, CancellationToken ct);
    public IRoomItem CreateFromFurnitureItemSnapshot(FurnitureItemSnapshot item);

    /// <summary>
    /// A floor item made from a definition alone, with no row behind it: a temporary furni. The
    /// caller gives it its id and says whose name it carries.
    /// </summary>
    public IRoomFloorItem CreateFloorItem(
        RoomObjectId objectId,
        PlayerId ownerId,
        FurnitureDefinitionSnapshot definition
    );

    /// <summary>
    /// The same for either kind, floor or wall, as the definition says. Borrowed furni is built
    /// this way: the row behind it is written by the room, not by an inventory.
    /// </summary>
    public IRoomItem CreateFromDefinition(
        RoomObjectId objectId,
        PlayerId ownerId,
        FurnitureDefinitionSnapshot definition
    );
}
