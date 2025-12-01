using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record SetTargetedOfferStateMessage : IMessageEvent
{
    public int TargetedOfferId { get; init; }
    public int TrackingState { get; init; }
}
