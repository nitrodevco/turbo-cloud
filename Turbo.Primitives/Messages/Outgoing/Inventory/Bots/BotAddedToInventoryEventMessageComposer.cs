using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Bots;

[GenerateSerializer, Immutable]
public sealed record BotAddedToInventoryEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
