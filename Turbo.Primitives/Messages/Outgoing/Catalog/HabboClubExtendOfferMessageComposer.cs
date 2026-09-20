using Orleans;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

/// <summary>
/// The renewal the club centre offers. Sending it opens the confirmation dialog
/// (<c>ClubExtendController.onOffer</c>), so it is only sent in answer to the request.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HabboClubExtendOfferMessageComposer : IComposer
{
    [Id(0)]
    public required ClubExtendOfferSnapshot Offer { get; init; }
}
