using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Bots;

[GenerateSerializer, Immutable]
public sealed record BotRemovedFromInventoryEventMessageComposer : IComposer
{
    [Id(0)]
    public required int BotId { get; init; }
}
