using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Trading;

[GenerateSerializer, Immutable]
public sealed record TradingYouAreNotAllowedEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
