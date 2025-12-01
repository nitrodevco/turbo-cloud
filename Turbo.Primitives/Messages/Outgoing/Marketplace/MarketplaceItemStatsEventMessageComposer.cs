using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Marketplace;

[GenerateSerializer, Immutable]
public sealed record MarketplaceItemStatsEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
