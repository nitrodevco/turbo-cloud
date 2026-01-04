using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Chat;

[GenerateSerializer, Immutable]
public sealed record ShoutMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
