using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Purse;

[GenerateSerializer, Immutable]
public sealed record CreditBalanceEventMessageComposer : IComposer
{
    [Id(0)]
    public required string Balance { get; init; }
}
