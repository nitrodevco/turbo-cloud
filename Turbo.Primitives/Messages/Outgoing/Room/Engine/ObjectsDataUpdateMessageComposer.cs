using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.StuffData;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ObjectsDataUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableDictionary<long, StuffDataSnapshot> StuffDatas { get; init; }
}
