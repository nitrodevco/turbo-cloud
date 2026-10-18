using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// Floor Switch 2 (<c>wf_floor_switch2</c>), the four-state switch of the same kind ("Can only be
/// operated from an adjacent tile" too): it behaves as Floor Switch 1 and steps through its
/// states 0, 1, 2, 3 and back to 0 (official client capture, 2026-10-18).
/// </summary>
[RoomObjectLogic("wf_floor_switch2")]
public class FurnitureFloorSwitch2Logic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorSwitchLogic(stuffDataFactory, ctx);
