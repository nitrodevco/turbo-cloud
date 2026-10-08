using System;
using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Variable;

/// <summary>
/// A variable's value: a 64-bit signed integer, as Habbo keeps them (the Wired Faculty's "Intro
/// to Bitwise Operations": "Habbo saves numbers as 64-bit signed integers"). The client is sent
/// 32 bits of it (<see cref="ToClient"/>); calculations - a time in milliseconds, a bit at
/// position 40 - keep all 64.
/// </summary>
[GenerateSerializer, Immutable]
public readonly record struct WiredVariableValue(long Value) : IComparable<WiredVariableValue>
{
    public int CompareTo(WiredVariableValue other) => Value.CompareTo(other.Value);

    public static WiredVariableValue Parse(long value) => new(value);

    /// <summary>The value as a client packet carries it: its low 32 bits.</summary>
    public int ToClient() => unchecked((int)Value);

    /// <summary>The value for a 32-bit game field (a state, a tile, a score): held within its range.</summary>
    public int ClampToInt() => (int)Math.Clamp(Value, int.MinValue, int.MaxValue);

    public static implicit operator long(WiredVariableValue value) => value.Value;

    public static explicit operator int(WiredVariableValue value) => unchecked((int)value.Value);

    public static implicit operator WiredVariableValue(long value) => new(value);

    public static WiredVariableValue Default => new(1);
}
