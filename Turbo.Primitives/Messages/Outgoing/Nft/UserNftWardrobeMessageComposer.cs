using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Nft;

/// <summary>The NFT outfits the player owns, as the avatar editor's NFT tab lists them.</summary>
[GenerateSerializer, Immutable]
public sealed record UserNftWardrobeMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<NftWardrobeItemSnapshot> Items { get; init; }
}

/// <summary>One NFT outfit (Flash <c>NftWardrobeItem</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record NftWardrobeItemSnapshot
{
    [Id(0)]
    public required string Id { get; init; }

    [Id(1)]
    public required string Figure { get; init; }

    [Id(2)]
    public required string Gender { get; init; }

    [Id(3)]
    public required string TokenId { get; init; }

    [Id(4)]
    public required string ContractKey { get; init; }
}
