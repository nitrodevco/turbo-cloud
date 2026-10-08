using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~area_hide.length</c>: The length of the area hidden, in tiles.</summary>
public sealed class FurnitureAreaHideLengthVariable(RoomGrain roomGrain)
    : FurnitureIndexedSmartVariable<FurnitureAreaHideLogic>(roomGrain)
{
    protected override string VariableName => "~area_hide.length";

    protected override ushort Order => 1;

    protected override int Index => 4;
}
