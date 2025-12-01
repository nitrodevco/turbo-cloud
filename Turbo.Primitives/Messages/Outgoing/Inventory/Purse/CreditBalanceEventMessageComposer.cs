using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Purse;

[GenerateSerializer, Immutable]
public sealed record CreditBalanceEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
