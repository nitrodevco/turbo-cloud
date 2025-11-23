using Orleans;
using Turbo.Primitives.Rooms.Mapping;

namespace Turbo.Primitives.Orleans.Snapshots.Room.Mapping;

[GenerateSerializer, Immutable]
public sealed record RoomTileSnapshot
{
    [Id(0)]
    public required byte X { get; init; }

    [Id(1)]
    public required byte Y { get; init; }

    [Id(2)]
    public required double Height { get; init; }

    [Id(3)]
    public required short RelativeHeight { get; init; }

    [Id(4)]
    public required RoomTileFlags Flags { get; init; }
}
