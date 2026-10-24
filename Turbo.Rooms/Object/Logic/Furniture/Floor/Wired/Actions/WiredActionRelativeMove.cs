using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Moves the picked furni by a fixed offset. Params: the signed horizontal and vertical tile
/// offsets, each within -20..20.
/// </summary>
[RoomObjectLogic("wf_act_rel_mov")]
public class WiredActionRelativeMove(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredMoveActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.RELATIVE_FURNI_MOVE;

    protected override bool AllowsWallFurni => true;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(-20, 20, 0), new WiredRangeParamRule(-20, 20, 0)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override void PlanMoves(IWiredContext ctx, WiredMovePlan plan)
    {
        var dx = GetIntParamOrDefault(0, 0);
        var dy = GetIntParamOrDefault(1, 0);

        if (dx == 0 && dy == 0)
            return;

        foreach (var item in GetFloorItems(ctx.GetSelection(this)))
        {
            var at = plan.Where(item);

            // Straight to the target, the tiles between not checked; a user there blocks it
            // (official client capture 2026-10-24, W1 lab action.33.R3 2/2, action.33.X5 3/3).
            if (IsBlockedByUser(ctx, item, at, at.X + dx, at.Y + dy, at.Rotation))
            {
                plan.Block();

                continue;
            }

            plan.Move(item, at.X + dx, at.Y + dy);
        }

        PlanWallItemSteps(ctx.GetSelection(this), plan, dx, dy);
    }
}
