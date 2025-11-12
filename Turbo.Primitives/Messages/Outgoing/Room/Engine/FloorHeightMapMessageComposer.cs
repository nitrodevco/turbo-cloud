using System.Collections.Generic;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Rooms;
using Turbo.Primitives.Snapshots.Rooms.Mapping;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record FloorHeightMapMessageComposer : IComposer
{
    [Id(0)]
    public required RoomScaleType ScaleType { get; init; }

    [Id(1)]
    public required int FixedWallsHeight { get; init; }

    [Id(2)]
    public required string ModelData { get; init; }

    [Id(3)]
    public required List<AreaHideDataSnapshot> AreaHideData { get; init; }
}
