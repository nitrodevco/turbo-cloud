using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Bots;

[GenerateSerializer, Immutable]
public sealed record BotRemovedFromInventoryEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
