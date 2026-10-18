using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

/// <summary>
/// The answer to <c>GetIsOfferGiftable</c>: the offer asked about and whether it can be bought as
/// a gift (<c>IsOfferGiftableMessageParser</c>: offerId, isGiftable).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record IsOfferGiftableMessageComposer : IComposer
{
    [Id(0)]
    public required int OfferId { get; init; }

    [Id(1)]
    public required bool IsGiftable { get; init; }
}
