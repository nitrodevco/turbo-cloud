using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ObjectsDataUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableDictionary<long, StuffDataSnapshot> StuffDatas { get; init; }
}
