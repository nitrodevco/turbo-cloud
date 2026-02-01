using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

[RoomObjectLogic("wf_trg_period_long")]
public class WiredTriggerLongPeriodically(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredTriggerPeriodically(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.PERIODIC_LONG;
    public override WiredPeriodicTriggerType PeriodicType => WiredPeriodicTriggerType.Long;
}
