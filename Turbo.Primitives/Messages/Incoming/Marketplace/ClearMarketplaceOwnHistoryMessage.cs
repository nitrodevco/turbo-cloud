using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Marketplace;

/// <summary>Flash <c>MarketPlaceLogic.clearOwnHistory</c>: clear the own offers of a category (2 sold, 3 expired).</summary>
public record ClearMarketplaceOwnHistoryMessage : IMessageEvent
{
    public required int Category { get; init; }
}
