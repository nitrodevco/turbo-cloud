using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Handshake;

public record CompleteDiffieHandshakeMessage : IMessageEvent
{
    public string SharedKey { get; init; }
}
