using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public class InitDiffieHandshakeComposer : IComposer
{
    public string Prime { get; init; }
    public string Generator { get; init; }
}
