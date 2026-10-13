using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Marketplace;

public record BuyMarketplaceOfferMessage : IMessageEvent
{
    public required int OfferId { get; init; }
}
