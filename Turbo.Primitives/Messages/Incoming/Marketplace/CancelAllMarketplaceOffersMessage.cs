using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Marketplace;

/// <summary>Flash <c>HabboCatalog.cancelAllMarketPlaceOffers</c>: take back every unsold own offer.</summary>
public record CancelAllMarketplaceOffersMessage : IMessageEvent { }
