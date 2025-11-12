using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Perk;

[GenerateSerializer, Immutable]
public sealed record PerkAllowancesMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
