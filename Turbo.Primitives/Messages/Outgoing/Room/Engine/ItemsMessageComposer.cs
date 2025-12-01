using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ItemsMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableDictionary<long, string> OwnerNames { get; init; }

    [Id(1)]
    public required ImmutableArray<RoomWallItemSnapshot> WallItems { get; init; }
}
