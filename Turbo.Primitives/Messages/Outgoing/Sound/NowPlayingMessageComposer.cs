using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Sound;

[GenerateSerializer, Immutable]
public sealed record NowPlayingMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
