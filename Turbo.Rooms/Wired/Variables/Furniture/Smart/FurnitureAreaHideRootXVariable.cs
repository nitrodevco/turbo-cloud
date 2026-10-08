using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~area_hide.root_x</c>: The area hider's root tile, along x.</summary>
public sealed class FurnitureAreaHideRootXVariable(RoomGrain roomGrain)
    : FurnitureIndexedSmartVariable<FurnitureAreaHideLogic>(roomGrain)
{
    protected override string VariableName => "~area_hide.root_x";

    protected override ushort Order => 4;

    protected override int Index => 1;
}
