using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// Floor Switch 2 (<c>wf_floor_switch2</c>), the four-state switch of the same kind ("Can only be
/// operated from an adjacent tile" too). It is taken to behave as Floor Switch 1 under the
/// Toggle Furni State effect; Habbo was only seen with Floor Switch 1.
/// </summary>
[RoomObjectLogic("wf_floor_switch2")]
public class FurnitureFloorSwitch2Logic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorSwitchLogic(stuffDataFactory, ctx);
