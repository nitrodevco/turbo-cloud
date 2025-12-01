using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Nux;

[GenerateSerializer, Immutable]
public sealed record SelectInitialRoomEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
