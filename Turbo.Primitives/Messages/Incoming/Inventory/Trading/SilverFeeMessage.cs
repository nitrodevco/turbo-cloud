using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Trading;

/// <summary>The player offers to pay the silver fee of a collectible (NFT) trade.</summary>
public record SilverFeeMessage : IMessageEvent
{
    public required bool Pay { get; init; }
}
