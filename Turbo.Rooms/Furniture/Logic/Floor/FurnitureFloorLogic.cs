using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Primitives.Actor;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.Logic.Floor;

[FurnitureLogic("default_floor")]
public class FurnitureFloorLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureLogicBase<IRoomFloorItem, IRoomFloorItemContext>(stuffDataFactory, ctx),
        IFurnitureFloorLogic
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

    public virtual bool CanStack() => _ctx.Definition.CanStack;

    public virtual bool CanWalk() => _ctx.Definition.CanWalk;

    public virtual bool CanSit() => _ctx.Definition.CanSit;

    public virtual bool CanLay() => _ctx.Definition.CanLay;

    public override async Task OnUseAsync(ActorContext ctx, int param, CancellationToken ct)
    {
        param = GetNextToggleableState();

        await SetStateAsync(param);
    }

    public virtual Task OnStopAsync(ActorContext ctx, CancellationToken ct) => Task.CompletedTask;

    protected virtual int GetNextToggleableState()
    {
        var totalStates = _ctx.Item.Definition.TotalStates;

        if (totalStates == 0 || StuffData is null)
            return 0;

        return (StuffData.GetState() + 1) % totalStates;
    }
}
