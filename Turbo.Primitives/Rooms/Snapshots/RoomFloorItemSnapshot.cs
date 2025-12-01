using Orleans;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.StuffData;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public sealed record RoomFloorItemSnapshot
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required long OwnerId { get; init; }

    [Id(2)]
    public required string OwnerName { get; init; }

    [Id(3)]
    public required int SpriteId { get; init; }

    [Id(4)]
    public required int X { get; init; }

    [Id(5)]
    public required int Y { get; init; }

    [Id(6)]
    public required double Z { get; init; }

    [Id(7)]
    public required Rotation Rotation { get; init; }

    [Id(8)]
    public required double StackHeight { get; init; }

    [Id(9)]
    public required StuffDataSnapshot StuffData { get; init; }

    [Id(10)]
    public required string StuffDataJson { get; init; }

    [Id(11)]
    public required FurniUsagePolicy UsagePolicy { get; init; }
}
