using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Trading.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Trading;

[GenerateSerializer, Immutable]
public sealed record TradingCloseEventMessageComposer : IComposer
{
    /// <summary>Who closed it; the other party gets a "trade closed" alert.</summary>
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required TradeCloseReasonType Reason { get; init; }
}
