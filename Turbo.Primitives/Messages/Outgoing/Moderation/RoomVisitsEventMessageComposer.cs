using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

[GenerateSerializer, Immutable]
public sealed record RoomVisitsEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
