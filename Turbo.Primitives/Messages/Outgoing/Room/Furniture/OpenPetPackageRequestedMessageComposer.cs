using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record OpenPetPackageRequestedMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }
}
