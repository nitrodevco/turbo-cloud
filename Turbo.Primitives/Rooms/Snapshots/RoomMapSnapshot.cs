using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public sealed record RoomMapSnapshot
{
    [Id(0)]
    public required string ModelName { get; init; }

    [Id(1)]
    public required string ModelData { get; init; }

    [Id(2)]
    public required int Width { get; init; }

    [Id(3)]
    public required int Height { get; init; }

    [Id(4)]
    public required int Size { get; init; }

    [Id(5)]
    public required int DoorX { get; init; }

    [Id(6)]
    public required int DoorY { get; init; }

    [Id(7)]
    public required Rotation DoorRotation { get; init; }

    [Id(8)]
    public required short[] TileEncodedHeights { get; init; }
}
