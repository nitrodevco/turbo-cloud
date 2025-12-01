using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

[GenerateSerializer, Immutable]
public sealed record ModeratorRoomInfoEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
