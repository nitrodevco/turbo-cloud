using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Collectibles;

/// <summary>A count, then that many <c>MintTokenOffer</c> (Flash parser collectibles/CollectibleMintTokenOffersMessageEventParser.as:15). Turbo keeps no wallets or NFTs (there is no web3 side), so the list is always empty: the count is 0 and no element shape is written yet.</summary>
[GenerateSerializer, Immutable]
public sealed record CollectibleMintTokenOffersMessageComposer : IComposer;
