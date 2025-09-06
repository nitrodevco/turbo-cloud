using Turbo.Packets.Abstractions;

namespace Turbo.Packets.Outgoing.Handshake;

public class InitDiffieHandshakeComposer : IComposer
{
    public string Prime { get; init; }
    public string Generator { get; init; }
}
