using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Room.Permissions;

[GenerateSerializer, Immutable]
public sealed record YouAreNotControllerMessageComposer : IComposer
{
    [Id(0)]
    public required RoomId RoomId { get; init; }
}
