using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Collectibles;

/// <summary>The collectibles hub asks for a wallet's collections (Flash <c>CollectionsTab.requestCollections</c>, the wallet address).</summary>
public record GetNftCollectionsMessage : IMessageEvent
{
    public required string WalletAddress { get; init; }
}
