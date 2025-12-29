using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Snapshots.Mapping;

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
    public required short EncodedHeight { get; init; }

    [Id(4)]
    public required RoomTileFlags Flags { get; init; }

    [Id(5)]
    public required RoomObjectId HighestObjectId { get; init; }

    [Id(6)]
    public required IReadOnlyList<RoomObjectId> FloorObjectIds { get; init; }

    [Id(7)]
    public required IReadOnlyList<RoomObjectId> AvatarObjectIds { get; init; }
}
