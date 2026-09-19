using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// The "else" branch of a stack: runs when the conditions of this stack fail, and asks the
/// target stacks to run their actions only when their own conditions fail.
/// </summary>
[RoomObjectLogic("wf_act_neg_call_stacks")]
public class WiredActionNegativeCallStacks(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredActionCallStacks(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.NEG_CALL_ANOTHER_STACK;

    public override bool IsNegative => true;

    protected override bool IsNegativeCall => true;
}
