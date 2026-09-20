using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// A second furni for the long periodic trigger, and nothing more: it is the same box, so it
/// keeps its twin's wired code and behaviour rather than declaring them again. Its furnidata
/// name is still a placeholder, so it is a leftover of an older hotel rather than a product.
/// </summary>
[RoomObjectLogic("wf_trg_at_time_long")]
public class WiredTriggerAtTimeLong(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredTriggerLongPeriodically(grainFactory, stuffDataFactory, ctx);
