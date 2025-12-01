using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Advertisement;

[GenerateSerializer, Immutable]
public sealed record InterstitialMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
