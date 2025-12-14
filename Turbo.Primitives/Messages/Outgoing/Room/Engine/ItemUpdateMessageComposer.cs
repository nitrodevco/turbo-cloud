using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ItemUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required RoomWallItemSnapshot WallItem { get; init; }
}
