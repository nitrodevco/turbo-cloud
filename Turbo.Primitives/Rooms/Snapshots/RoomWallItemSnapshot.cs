using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public sealed record RoomWallItemSnapshot : RoomItemSnapshot
{
    [Id(0)]
    public required int WallOffset { get; init; }

    [Id(1)]
    public required string WallPosition { get; init; }
}
