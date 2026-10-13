using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~area_hide.hiding_wallitems</c>: Whether the wall items over the area are hidden too (1) or not (0).</summary>
public sealed class FurnitureAreaHideHidingWallItemsVariable(RoomGrain roomGrain)
    : FurnitureAreaHideFlagVariable(roomGrain)
{
    protected override string VariableName => "~area_hide.hiding_wallitems";

    protected override int Index => 6;
}
