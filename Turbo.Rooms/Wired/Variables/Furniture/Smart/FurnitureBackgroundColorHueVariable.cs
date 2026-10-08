using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary><c>~background_color.hue</c>: The background toner's hue, 0..255.</summary>
public sealed class FurnitureBackgroundColorHueVariable(RoomGrain roomGrain)
    : FurnitureIndexedSmartVariable<FurnitureBackgroundTonerLogic>(roomGrain)
{
    protected override string VariableName => "~background_color.hue";

    protected override ushort Order => 7;

    protected override int Index => 1;

    protected override int Max => 255;
}
