using System;
using System.Globalization;
using Orleans;

namespace Turbo.Primitives.Players;

[GenerateSerializer, Immutable]
public readonly record struct PlayerId(int Value) : IComparable<PlayerId>
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public int CompareTo(PlayerId other) => Value.CompareTo(other.Value);

    public static PlayerId Parse(int value) => new(value);

    public static implicit operator int(PlayerId id) => id.Value;

    public static implicit operator PlayerId(int value) => new(value);

    public static PlayerId Invalid => new(-1);
}
