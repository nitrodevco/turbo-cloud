using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~area_hide.root_y</c>: The area hider's root tile, along y.</summary>
public sealed class FurnitureAreaHideRootYVariable(RoomGrain roomGrain)
    : FurnitureIndexedSmartVariable<FurnitureAreaHideLogic>(roomGrain)
{
    protected override string VariableName => "~area_hide.root_y";

    protected override ushort Order => 3;

    protected override int Index => 2;
}
