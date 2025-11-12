using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Trading;

[GenerateSerializer, Immutable]
public sealed record TradingNotOpenEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
