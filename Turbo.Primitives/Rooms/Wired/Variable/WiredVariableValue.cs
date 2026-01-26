using System;
using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Variable;

[GenerateSerializer, Immutable]
public readonly record struct WiredVariableValue(int Value) : IComparable<WiredVariableValue>
{
    public int CompareTo(WiredVariableValue other) => Value.CompareTo(other.Value);

    public static WiredVariableValue Parse(int value) => new(value);

    public static implicit operator int(WiredVariableValue id) => id.Value;

    public static implicit operator WiredVariableValue(int value) => new(value);

    public static WiredVariableValue Default => new(1);
}
