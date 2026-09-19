using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Trading;

/// <summary>Sent to both parties; the client works out which side is its own.</summary>
[GenerateSerializer, Immutable]
public sealed record TradingOpenEventMessageComposer : IComposer
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required bool PlayerCanTrade { get; init; }

    [Id(2)]
    public required PlayerId OtherPlayerId { get; init; }

    [Id(3)]
    public required bool OtherPlayerCanTrade { get; init; }
}
