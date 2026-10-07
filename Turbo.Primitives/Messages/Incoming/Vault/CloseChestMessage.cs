using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Vault;

/// <summary>The user closes a wired chest's window.</summary>
public record CloseChestMessage : IMessageEvent
{
    public required int ChestId { get; init; }
}
