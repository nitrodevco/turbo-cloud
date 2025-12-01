using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

[GenerateSerializer, Immutable]
public sealed record RoomQueueStatusMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
