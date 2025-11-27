using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.StuffData;
using Turbo.Primitives.Rooms.Furniture.Wall;

namespace Turbo.Rooms.Furniture.Logic.Wall;

[FurnitureLogic("default_wall")]
public class FurnitureWallLogic
    : FurnitureLogicBase<IRoomWallItem, IRoomWallItemContext>,
        IFurnitureWallLogic
{
    protected string _stuffData = string.Empty;

    public FurnitureWallLogic(IStuffDataFactory stuffDataFactory, IRoomWallItemContext ctx)
        : base(stuffDataFactory, ctx)
    {
        _stuffData = _ctx.Item.PendingStuffDataRaw;
    }

    public string StuffData => _stuffData;

    public override async Task<bool> SetStateAsync(int state)
    {
        if (_stuffData.Equals(state.ToString()))
            return false;

        _stuffData = state.ToString();

        _ctx.Item.MarkDirty();

        await _ctx.RefreshStuffDataAsync(CancellationToken.None);

        return true;
    }
}
