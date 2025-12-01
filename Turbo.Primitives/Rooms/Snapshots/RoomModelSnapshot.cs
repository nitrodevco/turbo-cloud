using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public sealed record RoomModelSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required string Model { get; init; }

    [Id(3)]
    public required int DoorX { get; init; }

    [Id(4)]
    public required int DoorY { get; init; }

    [Id(5)]
    public required Rotation DoorRotation { get; init; }

    [Id(6)]
    public required int Width { get; init; }

    [Id(7)]
    public required int Height { get; init; }

    [Id(8)]
    public required int Size { get; init; }

    [Id(9)]
    public required double[] BaseHeights { get; init; }

    [Id(10)]
    public required RoomTileFlags[] BaseFlags { get; init; }
}
