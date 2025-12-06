using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;

namespace Turbo.Rooms.Object.Logic.Furniture.Wall;

[RoomObjectLogic("default_wall")]
public class FurnitureWallLogic
    : FurnitureLogicBase<IRoomWallItem, IRoomWallItemContext>,
        IFurnitureWallLogic
{
    public IRoomWallItemContext Context => _ctx;

    public FurnitureWallLogic(IStuffDataFactory stuffDataFactory, IRoomWallItemContext ctx)
        : base(stuffDataFactory, ctx)
    {
        _stuffData = CreateStuffData(_ctx.Item.PendingStuffDataRaw);

        _stuffData.SetAction(() => _ctx.Item.MarkDirty());
    }

    public override Task<int> GetStateAsync() => Task.FromResult(_stuffData.GetState());

    public override async Task<bool> SetStateAsync(int state)
    {
        _stuffData.SetState(state.ToString());

        await _ctx.RefreshStuffDataAsync(CancellationToken.None);

        return true;
    }
}
