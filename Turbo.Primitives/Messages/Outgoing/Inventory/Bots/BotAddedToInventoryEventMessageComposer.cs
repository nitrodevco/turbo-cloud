using Orleans;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Bots;

[GenerateSerializer, Immutable]
public sealed record BotAddedToInventoryEventMessageComposer : IComposer
{
    [Id(0)]
    public required BotSnapshot Bot { get; init; }

    [Id(1)]
    public required bool OpenInventory { get; init; }
}
