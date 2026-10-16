using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Collectibles;

/// <summary>How many mint tokens a wallet holds (Flash parser collectibles/CollectibleMintTokenCountMessageEventParser.as:15).</summary>
[GenerateSerializer, Immutable]
public sealed record CollectibleMintTokenCountMessageComposer : IComposer
{
    [Id(0)]
    public required int Count { get; init; }
}
