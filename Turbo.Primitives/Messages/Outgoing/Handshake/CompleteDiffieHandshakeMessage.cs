using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public record CompleteDiffieHandshakeMessageComposer : IComposer
{
    public required string PublicKey { get; init; }
    public bool ServerClientEncryption { get; init; }
}
