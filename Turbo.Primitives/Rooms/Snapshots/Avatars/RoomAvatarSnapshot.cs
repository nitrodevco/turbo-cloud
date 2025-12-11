using Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Snapshots.Avatars;

[GenerateSerializer, Immutable]
public record RoomAvatarSnapshot
{
    [Id(0)]
    public required int WebId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required string Motto { get; init; }

    [Id(3)]
    public required string Figure { get; init; }

    [Id(4)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(5)]
    public required int X { get; init; }

    [Id(6)]
    public required int Y { get; init; }

    [Id(7)]
    public required double Z { get; init; }

    [Id(8)]
    public required Rotation BodyRotation { get; init; }

    [Id(9)]
    public required Rotation HeadRotation { get; init; }

    [Id(10)]
    public required string Status { get; init; }

    [Id(11)]
    public required int GoalTileId { get; init; }

    [Id(12)]
    public required int NextTileId { get; init; }

    [Id(13)]
    public required bool IsWalking { get; init; }
}
