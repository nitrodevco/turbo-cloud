using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;

[RoomObjectLogic("wf_game_upcounter1")]
public sealed class FurnitureGameCounter1Logic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureGameCounterLogic(stuffDataFactory, ctx);
