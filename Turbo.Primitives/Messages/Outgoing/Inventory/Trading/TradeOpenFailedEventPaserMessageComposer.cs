using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Trading.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Trading;

[GenerateSerializer, Immutable]
public sealed record TradeOpenFailedEventPaserMessageComposer : IComposer
{
    [Id(0)]
    public required TradeOpenFailedType Reason { get; init; }

    [Id(1)]
    public required string OtherPlayerName { get; init; }
}
