using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary>
/// <c>~recolorable_furni.color.rgb.blue</c>: one channel of the colour, 0..255, read only, as the
/// official server lists it (WIN63-202610121624 capture: hasValue only, no text connector).
/// </summary>
public sealed class FurnitureRecolorableBlueVariable(RoomGrain roomGrain)
    : FurnitureRecolorableColorVariable(roomGrain)
{
    protected override string VariableName => "~recolorable_furni.color.rgb.blue";

    protected override ushort Order => 3;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    protected override int Read(int rgb) => RecolorableData.Blue(rgb);

    protected override int? Write(int rgb, long value) => null;
}
