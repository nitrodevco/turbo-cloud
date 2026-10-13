using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~area_hide.is_invisible_furni</c>: Whether the furni in the area are hidden too (1) or not (0).</summary>
public sealed class FurnitureAreaHideInvisibleFurniVariable(RoomGrain roomGrain)
    : FurnitureAreaHideFlagVariable(roomGrain)
{
    protected override string VariableName => "~area_hide.is_invisible_furni";

    protected override int Index => 5;
}
