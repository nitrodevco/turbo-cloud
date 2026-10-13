using Orleans;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Marketplace;

[GenerateSerializer, Immutable]
public sealed record MarketplaceBuyOfferResultEventMessageComposer : IComposer
{
    [Id(0)]
    public required MarketplaceBuyResultCode Result { get; init; }

    [Id(1)]
    public required int OfferId { get; init; }

    [Id(2)]
    public required int NewPrice { get; init; }

    [Id(3)]
    public required int RequestedOfferId { get; init; }
}
