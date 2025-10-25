using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public record InitDiffieHandshakeMessageComposer : IComposer
{
    public required string Prime { get; init; }
    public required string Generator { get; init; }
}
