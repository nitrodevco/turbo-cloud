using Turbo.Primitives;

namespace Turbo.Packets.Incoming.Handshake;

public record CompleteDiffieHandshakeMessage : IMessageEvent
{
    public string SharedKey { get; init; }
}
