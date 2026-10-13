using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Marketplace;

[GenerateSerializer, Immutable]
public sealed record MarketplaceClearOwnHistoryResultMessageComposer : IComposer
{
    [Id(0)]
    public required bool Success { get; init; }
}
