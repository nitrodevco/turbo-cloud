using Orleans;
using Turbo.Primitives.Catalog.TargetedOffers;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record TargetedOfferEventMessageComposer : IComposer
{
    [Id(0)]
    public required TargetedOfferSnapshot Offer { get; init; }
}
