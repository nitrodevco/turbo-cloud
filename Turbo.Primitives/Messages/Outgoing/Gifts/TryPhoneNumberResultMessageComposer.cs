using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Gifts;

[GenerateSerializer, Immutable]
public sealed record TryPhoneNumberResultMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
