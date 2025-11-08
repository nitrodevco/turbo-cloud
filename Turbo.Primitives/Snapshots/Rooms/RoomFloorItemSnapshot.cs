using Orleans;
using Turbo.Contracts.Enums.Rooms.Object;

namespace Turbo.Primitives.Snapshots.Rooms;

[GenerateSerializer, Immutable]
public sealed record RoomFloorItemSnapshot
{
    [Id(0)]
    public required long Id { get; init; }

    [Id(1)]
    public required int SpriteId { get; init; }

    [Id(2)]
    public required int X { get; init; }

    [Id(3)]
    public required int Y { get; init; }

    [Id(4)]
    public required float Z { get; init; }

    [Id(5)]
    public required Rotation Rotation { get; init; }

    [Id(6)]
    public required float StackHeight { get; init; }
}
