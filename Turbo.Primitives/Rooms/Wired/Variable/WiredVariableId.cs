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

    /// <summary>For ids a client sent: anything that is not a plain decimal number is refused.</summary>
    public static bool TryParse(string? decimalString, out WiredVariableId id)
    {
        var parsed = ulong.TryParse(
            decimalString,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var value
        );

        id = new(value);

        return parsed;
    }

    public string ToHexString() => Value.ToString("X16", CultureInfo.InvariantCulture);

    public static WiredVariableId ParseHex(string hexString) =>
        new(ulong.Parse(hexString, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
}
