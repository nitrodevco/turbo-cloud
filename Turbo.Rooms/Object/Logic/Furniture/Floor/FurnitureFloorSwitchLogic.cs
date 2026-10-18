using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// Floor Switch 1 (<c>wf_floor_switch1</c>), a switch players flip ("Can only be operated from an
/// adjacent tile"). The Toggle Furni State effect leaves it as it is: Habbo's effect ran, toggled
/// the lamp it also pointed at and left the switch out of its state update, 2 of 2 (official
/// client capture, 2026-10-18). Its other states and wired effects are a plain furni's.
/// </summary>
[RoomObjectLogic("wf_floor_switch1")]
public class FurnitureFloorSwitchLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override bool CanWiredToggle() => false;
}
