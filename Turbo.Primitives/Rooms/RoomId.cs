using Orleans;

namespace Turbo.Primitives.Rooms;

[GenerateSerializer, Immutable]
public readonly record struct RoomId
{
    [Id(0)]
    public int Value { get; init; }

    public RoomId(int value)
    {
        Value = value;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator int(RoomId id) => id.Value;

    public static implicit operator RoomId(int value) => new(value);
}
