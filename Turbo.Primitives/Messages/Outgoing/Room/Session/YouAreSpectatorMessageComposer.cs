using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

[GenerateSerializer, Immutable]
public sealed record YouAreSpectatorMessageComposer : IComposer
{
    [Id(0)]
    public required RoomId RoomId { get; init; }
}
