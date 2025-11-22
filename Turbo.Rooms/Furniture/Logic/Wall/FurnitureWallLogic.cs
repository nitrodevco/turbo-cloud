using System.Threading;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.StuffData;
using Turbo.Primitives.Rooms.Furniture.Wall;

namespace Turbo.Rooms.Furniture.Logic.Wall;

[FurnitureLogic("default_wall")]
public class FurnitureWallLogic(IStuffDataFactory stuffDataFactory, IRoomWallItemContext ctx)
    : FurnitureLogicBase<IRoomWallItem, IRoomWallItemContext>(stuffDataFactory, ctx),
        IFurnitureWallLogic
{
    public override bool SetState(int state)
    {
        var stuffData = _ctx.Item.StuffData;

        if (stuffData is null || state == stuffData.GetState())
            return false;

        stuffData.SetState(state.ToString());

        _ctx.MarkItemDirty();

        _ = _ctx.RefreshStuffDataAsync(CancellationToken.None);

        return true;
    }
}
