using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~background_color.saturation</c>: The background toner's saturation, 0..255.</summary>
public sealed class FurnitureBackgroundColorSaturationVariable(RoomGrain roomGrain)
    : FurnitureIndexedSmartVariable<FurnitureBackgroundTonerLogic>(roomGrain)
{
    protected override string VariableName => "~background_color.saturation";

    protected override ushort Order => 6;

    protected override int Index => 2;

    protected override int Max => 255;
}
