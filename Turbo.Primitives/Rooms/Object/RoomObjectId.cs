using System;
using System.Globalization;
using Orleans;

namespace Turbo.Primitives.Rooms.Object;

[GenerateSerializer, Immutable]
public readonly record struct RoomObjectId(int Value) : IComparable<RoomObjectId>
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public int CompareTo(RoomObjectId other) => Value.CompareTo(other.Value);

    public static RoomObjectId Parse(int value) => new(value);

    public static implicit operator int(RoomObjectId id) => id.Value;

    public static implicit operator RoomObjectId(int value) => new(value);
}
