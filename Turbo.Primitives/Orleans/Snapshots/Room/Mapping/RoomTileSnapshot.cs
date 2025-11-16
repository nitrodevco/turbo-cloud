using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Room.Mapping;

[GenerateSerializer, Immutable]
public sealed record RoomTileSnapshot
{
    [Id(0)]
    public required byte X { get; init; }

    [Id(1)]
    public required byte Y { get; init; }

    [Id(2)]
    public required short RelativeHeight { get; init; }
}
