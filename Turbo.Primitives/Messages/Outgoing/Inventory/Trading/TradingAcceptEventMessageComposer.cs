using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Trading;

[GenerateSerializer, Immutable]
public sealed record TradingAcceptEventMessageComposer : IComposer
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required bool Accepted { get; init; }
}
