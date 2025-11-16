using System.Collections.Immutable;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record HeightMapUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<RoomTileSnapshot> Tiles { get; init; }
}
