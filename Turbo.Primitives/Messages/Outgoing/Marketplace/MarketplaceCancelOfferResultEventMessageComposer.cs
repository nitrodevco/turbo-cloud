using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Marketplace;

[GenerateSerializer, Immutable]
public sealed record MarketplaceCancelOfferResultEventMessageComposer : IComposer
{
    [Id(0)]
    public required int OfferId { get; init; }

    [Id(1)]
    public required bool Success { get; init; }
}
