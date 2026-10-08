using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~area_hide.width</c>: The width of the area hidden, in tiles.</summary>
public sealed class FurnitureAreaHideWidthVariable(RoomGrain roomGrain)
    : FurnitureIndexedSmartVariable<FurnitureAreaHideLogic>(roomGrain)
{
    protected override string VariableName => "~area_hide.width";

    protected override ushort Order => 2;

    protected override int Index => 3;
}
