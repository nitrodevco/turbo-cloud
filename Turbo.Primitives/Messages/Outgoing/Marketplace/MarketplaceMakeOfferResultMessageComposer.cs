using Orleans;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Marketplace;

[GenerateSerializer, Immutable]
public sealed record MarketplaceMakeOfferResultMessageComposer : IComposer
{
    [Id(0)]
    public required MarketplaceMakeOfferCode Result { get; init; }
}
