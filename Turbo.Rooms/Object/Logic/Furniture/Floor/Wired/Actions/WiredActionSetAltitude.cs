using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Sets, raises or lowers the altitude of the picked furni. Params: altitude in hundredths
/// of a tile and the operator. Bounded by the room stack height.
/// </summary>
[RoomObjectLogic("wf_act_set_altitude")]
public class WiredActionSetAltitude(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredMoveActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.SET_FURNI_ALTITUDE;

    // After every other effect (RoomWiredSystem.ChooseActions).
    public override int RunOrder => int.MaxValue;

    protected override bool AllowsWallFurni => true;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            // Out of range is kept at the nearest end, not refused: 9000 and 20000 are stored as
            // 8000, -300 as 0 (official client capture 2026-10-24, W1 lab action.29.E1, 3/3).
            new WiredClampParamRule(0, 8000, 0),
            new WiredEnumParamRule<WiredOperatorType>(WiredOperatorType.Add),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override void PlanMoves(IWiredContext ctx, WiredMovePlan plan)
    {
        var amount = GetIntParamOrDefault(0, 0);
        var op = GetIntParamOrDefault(1, WiredOperatorType.Add);
        var maxStackHeight = RoomConfig.MaxStackHeight.ToInt();

        PlanWallItemsInPlace(ctx.GetSelection(this), plan);

        foreach (var item in GetFloorItems(ctx.GetSelection(this)))
        {
            // The furni's top stays within the room's stack height: set 80.00 put a block 1.00
            // high at 79.00 and a box 0.65 high at 79.35 (official client capture 2026-10-24, W1
            // lab action.29.L1, 3/3).
            var maxAltitude = Math.Max(0, maxStackHeight - item.Definition.StackHeight.ToInt());
            var current = plan.Where(item).Z.ToInt();
            var next = op switch
            {
                WiredOperatorType.Add => current + amount,
                WiredOperatorType.Subtract => current - amount,
                _ => amount,
            };

            next = Math.Clamp(next, 0, maxAltitude);

            if (next == current)
                continue;

            // Not a placement: the furni stays on its own tile and only its height changes, which
            // is this box's to set, so there is nothing to ask about and nothing to block.
            plan.SetAltitude(item, Altitude.FromInt(next));
        }
    }
}
