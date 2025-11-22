using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.StuffData;
using Turbo.Primitives.Rooms.Furniture.Wall;

namespace Turbo.Rooms.Furniture.Logic.Wall;

[FurnitureLogic("default_wall")]
public class FurnitureWallLogic(IStuffDataFactory stuffDataFactory, IRoomWallItemContext ctx)
    : FurnitureLogicBase<IRoomWallItemContext>(stuffDataFactory, ctx),
        IFurnitureWallLogic { }
