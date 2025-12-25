using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;

namespace Turbo.Rooms.Object.Logic.Furniture.Wall;

[RoomObjectLogic("default_wall")]
public class FurnitureWallLogic(IStuffDataFactory stuffDataFactory, IRoomWallItemContext ctx)
    : FurnitureLogicBase<IRoomWallItem, IRoomWallItemContext>(stuffDataFactory, ctx),
        IFurnitureWallLogic { }
