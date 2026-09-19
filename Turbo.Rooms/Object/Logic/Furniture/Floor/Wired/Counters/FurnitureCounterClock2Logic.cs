using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;

[RoomObjectLogic("wf_upcounter2")]
public sealed class FurnitureCounterClock2Logic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureCounterClockLogic(stuffDataFactory, ctx);
