using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>Writes its line when the conditions of the stack fail.</summary>
[RoomObjectLogic("wf_act_neg_log")]
public class WiredActionNegativeWriteToLogs(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredActionWriteToLogs(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.NEG_WRITE_TO_LOGS;

    public override bool IsNegative => true;
}
