using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Badges;

[GenerateSerializer, Immutable]
public sealed record IsBadgeRequestFulfilledEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
