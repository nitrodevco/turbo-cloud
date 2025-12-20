using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record RequestSpamWallPostItMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ItemId { get; init; }

    [Id(1)]
    public required string Location { get; init; }
}
