using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Handshake;

public record SSOTicketMessage : IMessageEvent
{
    public required string SSO { get; init; }
    public int ElapsedMilliseconds { get; init; }
}
