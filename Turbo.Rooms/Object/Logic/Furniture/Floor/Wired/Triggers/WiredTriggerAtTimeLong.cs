using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>The second furniture of the long periodic trigger (five-second steps).</summary>
[RoomObjectLogic("wf_trg_at_time_long")]
public class WiredTriggerAtTimeLong(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredTriggerPeriodically(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.PERIODIC_LONG;
    public override WiredPeriodicTriggerType PeriodicType => WiredPeriodicTriggerType.Long;
}
