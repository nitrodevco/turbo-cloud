using Orleans;

namespace Turbo.Primitives.Rooms.Object;

[GenerateSerializer, Immutable]
public readonly record struct RoomObjectId
{
    [Id(0)]
    public int Value { get; init; }

    public RoomObjectId(int value)
    {
        Value = value;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator int(RoomObjectId id) => id.Value;

    public static implicit operator RoomObjectId(int value) => new(value);
}
