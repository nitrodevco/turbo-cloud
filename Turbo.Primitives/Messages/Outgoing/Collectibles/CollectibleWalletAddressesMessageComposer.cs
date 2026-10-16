using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Collectibles;

/// <summary>A player's wallets (Flash parser collectibles/CollectibleWalletAddressesMessageEventParser.as:18): the Stardust wallet ("" for none), then every other one.</summary>
[GenerateSerializer, Immutable]
public sealed record CollectibleWalletAddressesMessageComposer : IComposer
{
    [Id(0)]
    public required string StardustWalletAddress { get; init; }

    [Id(1)]
    public required ImmutableArray<string> WalletAddresses { get; init; }
}
