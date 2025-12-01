using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Pets;

[GenerateSerializer, Immutable]
public sealed record PetInventoryEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
