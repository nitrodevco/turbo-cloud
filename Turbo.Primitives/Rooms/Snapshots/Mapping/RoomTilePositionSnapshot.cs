using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Mapping;

[GenerateSerializer, Immutable]
public sealed record RoomTilePositionSnapshot
{
    [Id(0)]
    public required int X { get; init; }

    [Id(1)]
    public required int Y { get; init; }
}
