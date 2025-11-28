using Orleans;

namespace Turbo.Primitives.Rooms;

[GenerateSerializer, Immutable]
public sealed record RoomId
{
    [Id(0)]
    public required long Value { get; init; }

    public static RoomId Empty => new() { Value = -1 };

    public bool IsEmpty() => Value < 0;

    public bool CompareTo(RoomId other) => Value == other.Value;

    public static RoomId From(long value)
    {
        if (value < 0)
            return Empty;

        return new() { Value = value };
    }
}
