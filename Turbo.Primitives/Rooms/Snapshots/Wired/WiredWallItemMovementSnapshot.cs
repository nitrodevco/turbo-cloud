using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

[GenerateSerializer, Immutable]
public sealed record WiredWallItemMovementSnapshot
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required bool IsDirectionRight { get; init; }

    [Id(2)]
    public required int SourceX { get; init; }

    [Id(3)]
    public required int SourceY { get; init; }

    [Id(4)]
    public required int SourceOffsetX { get; init; }

    [Id(5)]
    public required int SourceOffsetY { get; init; }

    [Id(6)]
    public required int TargetX { get; init; }

    [Id(7)]
    public required int TargetY { get; init; }

    [Id(8)]
    public required int TargetOffsetX { get; init; }

    [Id(9)]
    public required int TargetOffsetY { get; init; }

    [Id(10)]
    public required int AnimationTime { get; init; }
}
