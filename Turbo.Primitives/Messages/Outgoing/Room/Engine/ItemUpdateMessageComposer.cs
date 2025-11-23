using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ItemUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required RoomWallItemSnapshot WallItem { get; init; }
}
