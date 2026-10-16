using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Collectibles;

/// <summary>Whether minting is open (Flash parser collectibles/CollectibleMintingEnabledMessageEventParser.as:15).</summary>
[GenerateSerializer, Immutable]
public sealed record CollectibleMintingEnabledMessageComposer : IComposer
{
    [Id(0)]
    public required bool Enabled { get; init; }
}
