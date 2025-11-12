using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

[GenerateSerializer, Immutable]
public sealed record IdentityAccountsEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
