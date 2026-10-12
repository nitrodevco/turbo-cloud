using System.Collections.Generic;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary>
/// <c>~recolorable_furni.color.rgb</c>: the colour packed, <c>(r &lt;&lt; 16) | (g &lt;&lt; 8) | b</c>
/// (Discord 2026-10-12 "packed variables"; official capture: 0xFF9900 for an orange block). The
/// only writable one of the four, with the text connector flag and no connectors, as the official
/// server lists it - the new client shows it as <c>#RRGGBB</c> only with that flag
/// (<c>Util.getPackedVariableText</c>).
/// </summary>
public sealed class FurnitureRecolorableRgbVariable(RoomGrain roomGrain)
    : FurnitureRecolorableColorVariable(roomGrain)
{
    protected override string VariableName => "~recolorable_furni.color.rgb";

    protected override ushort Order => 6;

    protected override WiredVariableFlags Flags => base.Flags | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() => [];

    protected override int Read(int rgb) => rgb;

    protected override int? Write(int rgb, long value) =>
        value is >= 0 and <= RecolorableData.WHITE ? (int)value : null;
}
