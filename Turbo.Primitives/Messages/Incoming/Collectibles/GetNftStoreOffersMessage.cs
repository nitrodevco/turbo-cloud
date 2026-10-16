using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Collectibles;

/// <summary>The shop tab asks for the NFT store's offers (Flash <c>ShopTab</c>, no arguments).</summary>
public record GetNftStoreOffersMessage : IMessageEvent { }
