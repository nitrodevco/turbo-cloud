using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Gifts;

[GenerateSerializer, Immutable]
public sealed record TryVerificationCodeResultMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
