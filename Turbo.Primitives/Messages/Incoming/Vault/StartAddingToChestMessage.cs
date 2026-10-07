using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Vault;

/// <summary>The user starts picking inventory items to put in a wired chest.</summary>
public record StartAddingToChestMessage : IMessageEvent
{
    public required int ChestId { get; init; }
}
