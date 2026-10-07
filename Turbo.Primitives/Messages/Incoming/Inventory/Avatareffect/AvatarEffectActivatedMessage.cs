using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Avatareffect;

public record AvatarEffectActivatedMessage : IMessageEvent
{
    /// <summary>
    /// The effect id the client names. It is untrusted: the grain checks the player owns it.
    /// Activated starts one of the player's waiting copies.
    /// </summary>
    public required int Type { get; init; }
}
