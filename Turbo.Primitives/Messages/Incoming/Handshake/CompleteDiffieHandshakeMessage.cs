using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Handshake;

public record CompleteDiffieHandshakeMessage : IMessageEvent
{
    public required string SharedKey { get; init; }
}
