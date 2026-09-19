using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Bots;

[GenerateSerializer, Immutable]
public sealed record BotInventoryEventMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<BotSnapshot> Bots { get; init; }
}
