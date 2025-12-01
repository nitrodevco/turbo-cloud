using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public sealed record CompleteDiffieHandshakeMessageComposer : IComposer
{
    public required string PublicKey { get; init; }
    public bool ServerClientEncryption { get; init; }
}
