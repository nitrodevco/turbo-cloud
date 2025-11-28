using Orleans;

namespace Turbo.Primitives.Rooms.Object;

[GenerateSerializer, Immutable]
public sealed record RoomObjectId
{
    [Id(0)]
    public required int Value { get; init; }

    public static RoomObjectId Empty => new() { Value = -1 };

    public static RoomObjectId From(int value) => new() { Value = value };
}
