using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Collectibles;

[GenerateSerializer, Immutable]
public sealed record NftBonusItemClaimResultMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
