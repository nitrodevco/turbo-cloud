using System.Collections.Immutable;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ItemsMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableDictionary<long, string> OwnerNames { get; init; }

    [Id(1)]
    public required ImmutableArray<RoomWallItemSnapshot> WallItems { get; init; }
}
