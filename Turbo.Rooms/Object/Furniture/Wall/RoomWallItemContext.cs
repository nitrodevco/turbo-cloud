using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object.Furniture.Wall;

public sealed class RoomWallItemContext(RoomGrain roomGrain, IRoomWallItem roomObject)
    : RoomItemContext<IRoomWallItem, IFurnitureWallLogic, IRoomWallItemContext>(
        roomGrain,
        roomObject
    ),
        IRoomWallItemContext { }
