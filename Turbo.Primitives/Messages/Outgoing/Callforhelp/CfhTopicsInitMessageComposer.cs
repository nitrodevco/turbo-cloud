using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Callforhelp;

[GenerateSerializer, Immutable]
public sealed record CfhTopicsInitMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
