using System.Collections.Generic;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ObjectsMessageComposer : IComposer
{
    [Id(0)]
    public required Dictionary<int, string> OwnerNames { get; init; }

    [Id(1)]
    public required IReadOnlyList<RoomFloorItemSnapshot> FloorItems { get; init; }
}
