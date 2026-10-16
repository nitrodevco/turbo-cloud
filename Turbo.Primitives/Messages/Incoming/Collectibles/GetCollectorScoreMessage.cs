using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Collectibles;

/// <summary>The collectibles hub asks for a wallet's collector score (Flash <c>CollectiblesView</c> on a wallet pick, the wallet address).</summary>
public record GetCollectorScoreMessage : IMessageEvent
{
    public required string WalletAddress { get; init; }
}
