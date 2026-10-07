using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Vault;

/// <summary>The user takes everything out of a wired chest.</summary>
public record WithdrawAllFromChestMessage : IMessageEvent
{
    public required int ChestId { get; init; }
}
