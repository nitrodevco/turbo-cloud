using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.Logic.Floor;

[FurnitureLogic("default_floor")]
public class FurnitureFloorLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureLogicBase<IRoomFloorItem, IRoomFloorItemContext>(stuffDataFactory, ctx),
        IFurnitureFloorLogic
{
    public override bool SetState(int state)
    {
        if (_stuffData is null || state == StuffData.GetState())
            return false;

        _stuffData.SetState(state.ToString());

        _ = _ctx.MarkItemDirtyAsync();
        _ = _ctx.RefreshStuffDataAsync(CancellationToken.None);

        return true;
    }

    public virtual bool CanStack() => _ctx.Definition.CanStack;

    public virtual bool CanWalk() => _ctx.Definition.CanWalk;

    public virtual bool CanSit() => _ctx.Definition.CanSit;

    public virtual bool CanLay() => _ctx.Definition.CanLay;

    public override bool CanToggle()
    {
        return true;
        if (GetUsagePolicy() == FurniUsagePolicy.Nobody)
            return false;

        if (GetUsagePolicy() == FurniUsagePolicy.Controller)
        {
            // check if rights or higher
            return false;
        }

        return true;
    }

    public virtual bool IsOpen() => CanWalk() || CanSit() || CanLay();

    public override Task OnUseAsync(int param, CancellationToken ct)
    {
        if (CanToggle())
        {
            param = GetNextToggleableState();

            SetState(param);
        }

        return Task.CompletedTask;
    }

    public override Task OnClickAsync(CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnStopAsync(CancellationToken ct) => Task.CompletedTask;

    protected virtual int GetNextToggleableState()
    {
        var totalStates = _ctx.Item.Definition.TotalStates;

        if (totalStates == 0 || StuffData is null)
            return 0;

        return (StuffData.GetState() + 1) % totalStates;
    }
}
