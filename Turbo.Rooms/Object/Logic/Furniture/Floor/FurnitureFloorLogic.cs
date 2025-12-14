using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

[RoomObjectLogic("default_floor")]
public class FurnitureFloorLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureLogicBase<IRoomFloorItem, IRoomFloorItemContext>(stuffDataFactory, ctx),
        IFurnitureFloorLogic
{
    public virtual bool CanStack() => _ctx.Definition.CanStack;

    public virtual bool CanWalk() => _ctx.Definition.CanWalk;

    public virtual bool CanSit() => _ctx.Definition.CanSit;

    public virtual bool CanLay() => _ctx.Definition.CanLay;

    public virtual bool CanRoll() => true;

    public virtual double GetPostureOffset()
    {
        if (CanSit())
            return _ctx.Definition.StackHeight;

        if (CanLay())
            return _ctx.Definition.StackHeight;

        return 0.0;
    }

    public virtual Task OnInvokeAsync(IRoomAvatarContext ctx, CancellationToken ct) =>
        Task.CompletedTask;
}
