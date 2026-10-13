using Orleans;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Marketplace;

[GenerateSerializer, Immutable]
public sealed record MarketplaceCanMakeOfferResultMessageComposer : IComposer
{
    [Id(0)]
    public required MarketplaceCanMakeOfferCode ResultCode { get; init; }

    [Id(1)]
    public required int TokenCount { get; init; }
}
