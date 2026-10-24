using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A gate: walkable while open. Neither a use nor wired changes it while someone stands in it:
/// the use gets no answer, and Toggle Furni State leaves it out of the state update while the
/// box still flashes (official client capture 2026-10-24, WIN63-202610201032, bc_gothic: empty it
/// toggled on every fire 3/3, with a user standing in it 0/2; action.0.B2, trigger.4.B2).
/// </summary>
[RoomObjectLogic("gate")]
public class FurnitureGateLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    public override bool CanWalk()
    {
        var state = StuffData.GetState();

        if (state == GateStates.OPEN)
            return true;

        return false;
    }

    public override bool CanChangeState() => !IsStoodOn();

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (!CanChangeState())
            return;

        await base.OnUseAsync(ctx, param, ct);
    }
}
