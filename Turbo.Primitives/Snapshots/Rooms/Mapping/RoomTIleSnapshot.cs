using Orleans;
using Turbo.Contracts.Enums.Rooms;

namespace Turbo.Primitives.Snapshots.Rooms.Mapping;

[GenerateSerializer]
public sealed record RoomTileSnapshot
{
    [Id(0)]
    public required int X { get; init; }

    [Id(1)]
    public required int Y { get; init; }

    [Id(2)]
    public required double Z { get; init; }

    [Id(3)]
    public required RoomTileStateType State { get; init; }

    [Id(4)]
    public required int[] UserIds { get; init; }

    [Id(5)]
    public required int[] FurniIds { get; init; }
}
