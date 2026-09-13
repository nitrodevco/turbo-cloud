using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Preferences;

public record SetIgnoreRoomInvitesMessage : IMessageEvent
{
    public bool IgnoreRoomInvites { get; init; }
}
