using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Room.Permissions;

[GenerateSerializer, Immutable]
public sealed record YouAreOwnerMessageComposer : IComposer
{
    [Id(0)]
    public required RoomId RoomId { get; init; }
}
