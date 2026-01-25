using System;
using System.Globalization;
using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Variable;

[GenerateSerializer, Immutable]
public readonly record struct WiredVariableId(ulong Value) : IComparable<WiredVariableId>
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public int CompareTo(WiredVariableId other) => Value.CompareTo(other.Value);

    public static WiredVariableId Parse(string decimalString) =>
        new(ulong.Parse(decimalString, NumberStyles.None, CultureInfo.InvariantCulture));

    public string ToHexString() => Value.ToString("X16", CultureInfo.InvariantCulture);

    public static WiredVariableId ParseHex(string hexString) =>
        new(ulong.Parse(hexString, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
}
