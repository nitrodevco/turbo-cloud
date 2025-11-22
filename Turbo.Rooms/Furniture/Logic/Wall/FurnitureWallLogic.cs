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
        if (_stuffData is null || state == _stuffData.GetState())
            return false;

        _stuffData.SetState(state.ToString());

        _ = _ctx.MarkItemDirtyAsync();
        _ = _ctx.RefreshStuffDataAsync(CancellationToken.None);

        return true;
    }
}
