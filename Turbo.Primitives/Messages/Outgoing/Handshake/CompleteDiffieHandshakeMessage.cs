using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public record CompleteDiffieHandshakeMessage : IComposer
{
    public required string PublicKey { get; init; }
    public bool ServerClientEncryption { get; init; }
}
