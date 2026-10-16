using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Collectibles;

/// <summary>The relics tab asks how many mint tokens a wallet holds (Flash <c>MintInventoryListTab</c>, the wallet address).</summary>
public record GetCollectibleMintTokensMessage : IMessageEvent
{
    public required string WalletAddress { get; init; }
}
