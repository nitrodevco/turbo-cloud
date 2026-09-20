using Orleans;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

/// <summary>
/// Answers a claimed gift. The client shows the first product as a "gift received" toast, so the
/// products are the point of it rather than the offer.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ClubGiftSelectedEventMessageComposer : IComposer
{
    [Id(0)]
    public required CatalogOfferSnapshot Offer { get; init; }
}
