using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record ShopTargetedOfferViewedMessage : IMessageEvent
{
    public int TargetedOfferId { get; init; }
    public int TrackingState { get; init; }
}
