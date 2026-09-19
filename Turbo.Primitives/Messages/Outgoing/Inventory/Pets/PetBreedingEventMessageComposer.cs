using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Pets;

/// <summary>Monsterplant breeding progress, from the receiving owner's point of view.</summary>
[GenerateSerializer, Immutable]
public sealed record PetBreedingEventMessageComposer : IComposer
{
    [Id(0)]
    public required PetBreedingState State { get; init; }

    [Id(1)]
    public required int OwnPetId { get; init; }

    [Id(2)]
    public required int OtherPetId { get; init; }
}
