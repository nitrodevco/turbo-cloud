using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Sound;

[GenerateSerializer, Immutable]
public sealed record PlayListMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
