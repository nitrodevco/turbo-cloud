using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

[GenerateSerializer, Immutable]
public sealed record InitDiffieHandshakeMessageComposer : IComposer
{
    [Id(0)]
    public required string Prime { get; init; }

    [Id(1)]
    public required string Generator { get; init; }
}
