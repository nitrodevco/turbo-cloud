using System;
using System.Globalization;
using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Variable;

[GenerateSerializer, Immutable]
public readonly record struct WiredVariableId(ulong Value) : IComparable<WiredVariableId>
{
    // Habbo's fixed ids for the variables every room has: "-120" for a furni's @position.x,
    // "~131" for ~chest.locked (WiredAllVariablesDiffs recorded on hhus, WIN63-202610121709).
    // They live in two tag ranges far above every hashed and box id, and print as Habbo's.
    private const ulong BUILT_IN_TAG = 0xFFFF_0000_0000_0000;
    private const ulong SMART_TAG = 0xFFFE_0000_0000_0000;
    private const ulong TAG_MASK = 0xFFFF_0000_0000_0000;
    private const ulong NUMBER_MASK = 0x0000_0000_FFFF_FFFF;

    /// <summary>Habbo's id "-<paramref name="number"/>" of a built-in (@) variable.</summary>
    public static WiredVariableId BuiltIn(int number) => new(BUILT_IN_TAG | (uint)number);

    /// <summary>Habbo's id "~<paramref name="number"/>" of a smart (~) variable.</summary>
    public static WiredVariableId Smart(int number) => new(SMART_TAG | (uint)number);

    public override string ToString() =>
        (Value & TAG_MASK) switch
        {
            BUILT_IN_TAG => "-" + (Value & NUMBER_MASK).ToString(CultureInfo.InvariantCulture),
            SMART_TAG => "~" + (Value & NUMBER_MASK).ToString(CultureInfo.InvariantCulture),
            _ => Value.ToString(CultureInfo.InvariantCulture),
        };

    public int CompareTo(WiredVariableId other) => Value.CompareTo(other.Value);

    public static WiredVariableId Parse(string decimalString) =>
        TryParse(decimalString, out var id) ? id : throw new FormatException(decimalString);

    /// <summary>
    /// For ids a client sent: a plain decimal number, or Habbo's "-N" / "~N"; anything else is
    /// refused.
    /// </summary>
    public static bool TryParse(string? decimalString, out WiredVariableId id)
    {
        if (decimalString is ['-' or '~', .. var rest])
        {
            var isNumber = int.TryParse(
                rest,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number
            );

            id =
                !isNumber ? new(0)
                : decimalString[0] == '-' ? BuiltIn(number)
                : Smart(number);

            return isNumber;
        }

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
