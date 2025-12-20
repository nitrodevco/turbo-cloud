using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ObjectsMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableDictionary<PlayerId, string> OwnerNames { get; init; }

    [Id(1)]
    public required ImmutableArray<RoomFloorItemSnapshot> FloorItems { get; init; }
}
