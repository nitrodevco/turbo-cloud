using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

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

    [Id(4)]
    public required int CameraInitX { get; init; }

    [Id(5)]
    public required int CameraInitY { get; init; }

    [Id(6)]
    public required Altitude CameraInitZ { get; init; }
}
