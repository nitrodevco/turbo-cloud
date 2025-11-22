using System.Collections.Generic;
using System.Collections.Immutable;
using Orleans;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;

namespace Turbo.Primitives.Orleans.Snapshots.Room.Mapping;

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
    public required short[] TileRelativeHeights { get; init; }

    [Id(9)]
    public required ImmutableArray<RoomFloorItemSnapshot> FloorItems { get; init; }

    [Id(10)]
    public required int Version { get; init; }
}
