using System.Collections.Immutable;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Snapshots.Room.StuffData;
using Turbo.Primitives.Rooms.StuffData;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ObjectsDataUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableDictionary<long, StuffDataSnapshot> StuffDatas { get; init; }
}
