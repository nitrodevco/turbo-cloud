using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO.Hashing;
using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

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

    // A box's variable: the database source in the bits above its 48-bit hash.
    private const ulong DATABASE_SOURCE =
        (ulong)((int)WiredVariableIdSourceType.Database & 0b1_1111) << 48;

    /// <summary>Habbo's id "-<paramref name="number"/>" of a built-in (@) variable.</summary>
    public static WiredVariableId BuiltIn(int number) => new(BUILT_IN_TAG | (uint)number);

    /// <summary>Habbo's id "~<paramref name="number"/>" of a smart (~) variable.</summary>
    public static WiredVariableId Smart(int number) => new(SMART_TAG | (uint)number);

    /// <summary>
    /// The id of the variable a wired box makes (a variable box's), from the box's furni id: what
    /// its saves and the room's variables name it by, the same on every server. A tool copying a
    /// room whose furni get new ids renames the variables by it.
    /// </summary>
    public static WiredVariableId OfBox(int boxId) => new(DATABASE_SOURCE | HashBoxId48(boxId));

    /// <summary>The id of the <paramref name="subIndex"/>-th variable a box derives (an add-on's), distinct from the box's own.</summary>
    public static WiredVariableId OfBox(int boxId, int subIndex) =>
        new(DATABASE_SOURCE | HashBoxId48(unchecked(boxId * 397) ^ (subIndex + 1) * 7919));

    private static ulong HashBoxId48(int boxId)
    {
        var hasher = new XxHash64();

        Span<byte> buf = stackalloc byte[4];

        BinaryPrimitives.WriteInt32BigEndian(buf, boxId);

        hasher.Append(buf);

        return hasher.GetCurrentHashAsUInt64() & 0x0000_FFFF_FFFF_FFFFUL;
    }

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
