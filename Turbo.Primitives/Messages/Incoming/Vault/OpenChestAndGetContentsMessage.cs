using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Vault;

/// <summary>The user opens a wired chest and asks for its contents.</summary>
public record OpenChestAndGetContentsMessage : IMessageEvent
{
    public required int ChestId { get; init; }
}
