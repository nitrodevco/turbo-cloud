using System;
using System.Globalization;
using Orleans;

namespace Turbo.Primitives.Rooms;

[GenerateSerializer, Immutable]
public readonly record struct RoomId(int Value) : IComparable<RoomId>
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public int CompareTo(RoomId other) => Value.CompareTo(other.Value);

    public static RoomId Parse(int value) => new(value);

    public static implicit operator int(RoomId id) => id.Value;

    public static implicit operator RoomId(int value) => new(value);

    public static RoomId Invalid => new(-1);
}
