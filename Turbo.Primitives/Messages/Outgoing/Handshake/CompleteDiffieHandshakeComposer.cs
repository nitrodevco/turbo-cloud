using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public class CompleteDiffieHandshakeComposer : IComposer
{
    public string PublicKey { get; init; }
}
