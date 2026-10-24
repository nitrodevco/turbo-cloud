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

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>Moves the furni of the first slot onto the tile of the furni in the second slot.</summary>
[RoomObjectLogic("wf_act_furni_to_furni")]
public class WiredActionFurniToFurni(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredMoveActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.MOVE_FURNI_TO_FURNI;

    protected override bool AllowsWallFurni => true;

    // The movers default to the triggering furni and the target to the picked furni (official
    // client capture); the secondary picks are offered on both.
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.TriggeredItem),
            WiredSources.FurniOrSecondary,
        ];

    // One destination drawn at random each firing; a blocked one is no move, not a retry
    // (official client capture 2026-10-24, W1 lab action.34.R8, 8 firings over 3 destinations;
    // action.34.B1, 3/3).
    protected override void PlanMoves(IWiredContext ctx, WiredMovePlan plan)
    {
        if (GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 1)).Count > 0)
            PlanWallItemsInPlace(WiredSlotSelection.ForSlot(this, ctx, 0), plan);

        PlanOntoTargetFurni(ctx, plan, 0, 0, anyTarget: true);
    }
}
