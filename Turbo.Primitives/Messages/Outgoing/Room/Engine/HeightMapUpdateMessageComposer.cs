using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record HeightMapUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<RoomTileSnapshot> Tiles { get; init; }
}
