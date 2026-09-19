using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Pets;

[GenerateSerializer, Immutable]
public sealed record PetReceivedMessageComposer : IComposer
{
    [Id(0)]
    public required bool BoughtAsGift { get; init; }

    [Id(1)]
    public required PetSnapshot Pet { get; init; }
}
