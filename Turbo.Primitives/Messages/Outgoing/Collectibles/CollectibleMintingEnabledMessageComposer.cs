using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Collectibles;

[GenerateSerializer, Immutable]
public sealed record CollectibleMintingEnabledMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
