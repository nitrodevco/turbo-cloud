using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public abstract class FurnitureFloorVariable(RoomGrain roomGrain)
    : FurnitureVariable<IRoomFloorItem>(roomGrain);
