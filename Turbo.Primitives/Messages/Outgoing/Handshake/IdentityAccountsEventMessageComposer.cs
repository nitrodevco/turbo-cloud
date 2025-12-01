using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

[GenerateSerializer, Immutable]
public sealed record IdentityAccountsEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
