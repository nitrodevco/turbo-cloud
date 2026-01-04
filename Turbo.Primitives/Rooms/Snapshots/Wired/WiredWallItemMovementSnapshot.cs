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
    public required int OldWallX { get; init; }

    [Id(3)]
    public required int OldWallY { get; init; }

    [Id(4)]
    public required int OldOffsetX { get; init; }

    [Id(5)]
    public required int OldOffsetY { get; init; }

    [Id(6)]
    public required int NewWallX { get; init; }

    [Id(7)]
    public required int NewWallY { get; init; }

    [Id(8)]
    public required int NewOffsetX { get; init; }

    [Id(9)]
    public required int NewOffsetY { get; init; }
}
