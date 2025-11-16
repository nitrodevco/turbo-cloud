using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ObjectUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required RoomFloorItemSnapshot FloorItem { get; init; }
}
