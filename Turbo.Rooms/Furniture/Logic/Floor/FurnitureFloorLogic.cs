using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.Logic.Floor;

[FurnitureLogic("default_floor")]
public class FurnitureFloorLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureLogicBase<IRoomFloorItemContext>(stuffDataFactory, ctx),
        IFurnitureFloorLogic
{
    public virtual bool CanWalk() => _ctx.Definition.CanWalk;

    public virtual bool CanSit() => _ctx.Definition.CanSit;

    public virtual bool CanLay() => _ctx.Definition.CanLay;

    public override bool CanToggle()
    {
        if (GetUsagePolicy() == FurniUsagePolicy.Nobody)
            return false;

        if (GetUsagePolicy() == FurniUsagePolicy.Controller)
        {
            // check if rights or higher
            return false;
        }

        return true;
    }

    public virtual bool IsOpen()
    {
        return CanWalk() || CanSit() || CanLay();
    }

    public override async Task OnInteractAsync(int param, CancellationToken ct)
    {
        if (!CanToggle())
            return;

        param = GetNextToggleableState();

        await SetStateAsync(param, ct);
    }

    protected virtual int GetNextToggleableState()
    {
        var totalStates = _ctx.Item.Definition.TotalStates;

        if (totalStates == 0)
            return 0;

        return (_ctx.Item.StuffData.GetState() + 1) % totalStates;
    }

    public virtual Task OnStopAsync(CancellationToken ct) => Task.CompletedTask;
}
