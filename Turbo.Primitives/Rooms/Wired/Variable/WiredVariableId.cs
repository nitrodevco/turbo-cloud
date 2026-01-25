using System.Globalization;
using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Variable;

[GenerateSerializer, Immutable]
public readonly record struct WiredVariableId(ulong Value)
{
    public override string ToString() => Value.ToString();

    public static WiredVariableId Parse(string hex) =>
        new(ulong.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
}
