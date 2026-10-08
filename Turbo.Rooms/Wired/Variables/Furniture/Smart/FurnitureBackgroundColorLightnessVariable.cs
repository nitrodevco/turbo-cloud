using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~background_color.lightness</c>: The background toner's lightness, 0..255.</summary>
public sealed class FurnitureBackgroundColorLightnessVariable(RoomGrain roomGrain)
    : FurnitureIndexedSmartVariable<FurnitureBackgroundTonerLogic>(roomGrain)
{
    protected override string VariableName => "~background_color.lightness";

    protected override ushort Order => 5;

    protected override int Index => 3;

    protected override int Max => 255;
}
