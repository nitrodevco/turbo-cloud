using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Vault;

/// <summary>The user buys more capacity for a wired chest.</summary>
public record UpgradeChestMessage : IMessageEvent
{
    public required int ChestId { get; init; }
    public required int UpgradeCount { get; init; }
}
