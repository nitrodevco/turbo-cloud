using Turbo.Packets.Abstractions;

namespace Turbo.Packets.Outgoing.Handshake;

public class CompleteDiffieHandshakeComposer : IComposer
{
    public string PublicKey { get; init; }
}
