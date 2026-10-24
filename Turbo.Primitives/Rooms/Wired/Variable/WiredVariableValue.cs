using System;
using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Variable;

/// <summary>
/// A variable's value: a 64-bit signed integer, as Habbo keeps them (the Wired Faculty's "Intro
/// to Bitwise Operations": "Habbo saves numbers as 64-bit signed integers"). The client is sent
/// it held within 32 bits (<see cref="ToClient"/>); calculations - a time in milliseconds, a bit
/// at position 40 - keep all 64.
/// </summary>
[GenerateSerializer, Immutable]
public readonly record struct WiredVariableValue(long Value) : IComparable<WiredVariableValue>
{
    public int CompareTo(WiredVariableValue other) => Value.CompareTo(other.Value);

    public static WiredVariableValue Parse(long value) => new(value);

    /// <summary>
    /// The value as a client packet carries it: held within 32 bits, so 2^32, 2^40 and 2^63-1 all
    /// read 2147483647 and -2^63 reads -2147483648 while the variable keeps the whole value
    /// (official client capture 2026-10-24, WIN63-202610201032, wired matrix action.41.B4/B4c/B4d).
    /// </summary>
    public int ToClient() => ClampToInt();

    /// <summary>The value for a 32-bit game field (a state, a tile, a score): held within its range.</summary>
    public int ClampToInt() => (int)Math.Clamp(Value, int.MinValue, int.MaxValue);

    public static implicit operator long(WiredVariableValue value) => value.Value;

    public static explicit operator int(WiredVariableValue value) => unchecked((int)value.Value);

    public static implicit operator WiredVariableValue(long value) => new(value);

    public static WiredVariableValue Default => new(1);

    /// <summary>
    /// What a held flag variable (one without a value of its own) reports: -1, as Habbo's
    /// inspection answers for <c>@is_stackable</c> and <c>@can_stand_on</c> (official client
    /// capture, WIN63-202610121624). The client draws no value for such a variable, but the menu
    /// and Web API read the number.
    /// </summary>
    public static WiredVariableValue HeldFlag => new(-1);
}
