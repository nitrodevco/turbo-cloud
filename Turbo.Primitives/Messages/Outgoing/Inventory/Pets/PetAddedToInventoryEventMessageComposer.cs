using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Pets;

[GenerateSerializer, Immutable]
public sealed record PetAddedToInventoryEventMessageComposer : IComposer
{
    [Id(0)]
    public required PetSnapshot Pet { get; init; }

    [Id(1)]
    public required bool OpenInventory { get; init; }
}
