using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Hotlooks;

[GenerateSerializer, Immutable]
public sealed record HotLooksMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
