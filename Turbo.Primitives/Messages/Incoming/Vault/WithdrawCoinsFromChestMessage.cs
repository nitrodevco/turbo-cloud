using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Vault;

/// <summary>The user takes coins out of a wired chest.</summary>
public record WithdrawCoinsFromChestMessage : IMessageEvent
{
    public required int ChestId { get; init; }
    public required int Amount { get; init; }
}
