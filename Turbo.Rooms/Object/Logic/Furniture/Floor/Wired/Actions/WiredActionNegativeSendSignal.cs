using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>Sends its signal when the conditions of the stack fail.</summary>
[RoomObjectLogic("wf_act_neg_send_signal")]
public class WiredActionNegativeSendSignal(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredActionSendSignal(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.NEG_SEND_SIGNAL;

    public override bool IsNegative => true;
}
