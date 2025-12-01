using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Callforhelp;

[GenerateSerializer, Immutable]
public sealed record CfhSanctionMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
