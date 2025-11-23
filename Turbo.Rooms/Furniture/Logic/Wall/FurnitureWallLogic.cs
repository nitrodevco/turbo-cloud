using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.StuffData;
using Turbo.Primitives.Rooms.Furniture.Wall;

namespace Turbo.Rooms.Furniture.Logic.Wall;

[FurnitureLogic("default_wall")]
public class FurnitureWallLogic(IStuffDataFactory stuffDataFactory, IRoomWallItemContext ctx)
    : FurnitureLogicBase<IRoomWallItem, IRoomWallItemContext>(stuffDataFactory, ctx),
        IFurnitureWallLogic
{
    public override async Task<bool> SetStateAsync(int state)
    {
        if (_stuffData is null || state == StuffData.GetState())
            return false;

        _stuffData.SetState(state.ToString());

        await _ctx.MarkItemDirtyAsync();
        await _ctx.RefreshStuffDataAsync(CancellationToken.None);

        return true;
    }
}
