using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// "Can Perform Movements": true when every furni move the stack's effects are about to make can
/// be made. The box has no options; the moves are the ones the effects that would run make, in
/// the order they would run them, each from where the moves before it left the furni
/// (<see cref="Turbo.Rooms.Grains.Systems.RoomWiredSystem"/> works them out before the conditions are asked). A failed move
/// fails the condition, and with it the stack.
/// </summary>
[RoomObjectLogic("wf_cnd_valid_moves")]
public class WiredConditionValidMoves(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.CAN_PERFORM_MOVE;

    protected override bool EvaluateCore(IWiredProcessingContext ctx) =>
        ctx is WiredProcessingContext { MovePlans: { } plans }
        && plans.Values.All(x => !x.HasBlockedMove);
}
