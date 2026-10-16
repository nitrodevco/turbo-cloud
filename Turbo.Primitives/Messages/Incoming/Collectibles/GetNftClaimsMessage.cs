using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Collectibles;

/// <summary>The rewards tab asks for a wallet's reward claims (Flash <c>RewardClaimsTab</c>, the wallet address).</summary>
public record GetNftClaimsMessage : IMessageEvent
{
    public required string WalletAddress { get; init; }
}
