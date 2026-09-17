using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

[GenerateSerializer, Immutable]
public sealed record CompleteDiffieHandshakeMessageComposer : IComposer
{
    [Id(0)]
    public required string PublicKey { get; init; }

    [Id(1)]
    public bool ServerClientEncryption { get; init; }
}
