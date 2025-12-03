using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots.Mapping;

[GenerateSerializer, Immutable]
public sealed record CompiledRoomModelSnapshot
{
    [Id(0)]
    public required int Width { get; init; }

    [Id(1)]
    public required int Height { get; init; }

    [Id(2)]
    public required double[] Heights { get; init; }

    [Id(3)]
    public required RoomTileFlags[] Flags { get; init; }
}
