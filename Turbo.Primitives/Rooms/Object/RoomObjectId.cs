using Orleans;

namespace Turbo.Primitives.Rooms.Object;

[GenerateSerializer, Immutable]
public sealed record RoomObjectId
{
    [Id(0)]
    public required int Value { get; init; }

    public static RoomObjectId Empty => new() { Value = -1 };

    public bool IsEmpty() => Value < 0;

    public bool CompareTo(RoomObjectId other) => Value == other.Value;

    public static RoomObjectId From(int value) => new() { Value = value };

    public override string ToString() => Value.ToString();
}
