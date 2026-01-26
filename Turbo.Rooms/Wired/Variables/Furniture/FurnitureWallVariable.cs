using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public abstract class FurnitureWallVariable(RoomGrain roomGrain)
    : FurnitureVariable<IRoomWallItem>(roomGrain);
