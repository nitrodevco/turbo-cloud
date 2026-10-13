using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Marketplace;

[GenerateSerializer, Immutable]
public sealed record MarketplaceCancelAllOffersResultMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<int> OfferIds { get; init; }

    [Id(1)]
    public required bool Success { get; init; }
}
