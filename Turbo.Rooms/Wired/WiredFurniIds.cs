using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Wired;

/// <summary>
/// How wired names furni to the client: a floor item by its id, a wall item by its id made
/// negative (Flash <c>HabboUserDefinedRoomEvents</c> picks a wall item as <c>-id</c> and reads a
/// negative id back as one; sirjonasxx, variables-info #11: "Wall items use negative identifiers").
/// The room keeps every item under its own, positive id.
/// </summary>
public static class WiredFurniIds
{
    /// <summary>The room's id for one the client sent: a negative one names that wall item.</summary>
    public static int FromClient(RoomFurniModule furni, int id) =>
        id < 0 && furni.TryGetItem(-id, out var item) && item is IRoomWallItem ? -id : id;

    public static int ToClient(IRoomItem item) =>
        item is IRoomWallItem ? -(int)item.ObjectId : (int)item.ObjectId;

    public static List<int> ToClient(RoomFurniModule furni, IEnumerable<int> ids) =>
        [.. ids.Select(id => furni.TryGetItem(id, out var item) ? ToClient(item) : id)];
}
