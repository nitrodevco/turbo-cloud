using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Collectibles;

/// <summary>A count, then that many <c>NftCollection</c> (its items, id, name, scores, bonus and reward items, times, status, claims) (Flash parser collectibles/NftCollectionsMessageEventParser.as:15). Turbo keeps no wallets or NFTs (there is no web3 side), so the list is always empty: the count is 0 and no element shape is written yet.</summary>
[GenerateSerializer, Immutable]
public sealed record NftCollectionsMessageComposer : IComposer;
