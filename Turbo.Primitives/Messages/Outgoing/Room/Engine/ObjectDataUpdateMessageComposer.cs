using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.StuffData;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ObjectDataUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required StuffDataSnapshot StuffData { get; init; }
}
