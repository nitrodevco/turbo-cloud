using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Marketplace;

/// <summary>Flash <c>GetMarketplaceOwnOffersMessageComposer</c>: the category asked for (1 open, 2 sold, 3 expired).</summary>
public record GetMarketplaceOwnOffersMessage : IMessageEvent
{
    public required int Category { get; init; }
}
