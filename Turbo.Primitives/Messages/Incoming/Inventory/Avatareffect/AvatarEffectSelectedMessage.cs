using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Avatareffect;

public record AvatarEffectSelectedMessage : IMessageEvent
{
    /// <summary>
    /// The effect id the client names. It is untrusted: the grain checks the player owns it.
    /// Selected also arrives as -1 (the official client) or 0 (Nitro) to take the worn effect off.
    /// </summary>
    public required int Type { get; init; }
}
