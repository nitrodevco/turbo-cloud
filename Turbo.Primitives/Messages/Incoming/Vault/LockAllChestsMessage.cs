using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Vault;

/// <summary>The user locks or unlocks wired chests: every chest they own, or those in the current room.</summary>
public record LockAllChestsMessage : IMessageEvent
{
    public required bool Lock { get; init; }
    public required bool All { get; init; }
}
