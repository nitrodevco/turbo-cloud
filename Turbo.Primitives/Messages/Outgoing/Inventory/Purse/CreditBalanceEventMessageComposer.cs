using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Purse;

[GenerateSerializer, Immutable]
public sealed record CreditBalanceEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
