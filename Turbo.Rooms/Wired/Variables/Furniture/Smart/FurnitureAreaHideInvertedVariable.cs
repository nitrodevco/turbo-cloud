using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~area_hide.inverted</c>: Whether everything but the area is hidden (1) or the area (0).</summary>
public sealed class FurnitureAreaHideInvertedVariable(RoomGrain roomGrain)
    : FurnitureIndexedSmartVariable<FurnitureAreaHideLogic>(roomGrain)
{
    protected override string VariableName => "~area_hide.inverted";

    protected override ushort Order => 0;

    protected override int Index => 7;

    protected override int Max => 1;
}
