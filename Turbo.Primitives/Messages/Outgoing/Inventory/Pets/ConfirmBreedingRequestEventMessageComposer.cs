using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Pets;

[GenerateSerializer, Immutable]
public sealed record ConfirmBreedingRequestEventMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId NestId { get; init; }

    [Id(1)]
    public required PetBreedingPetSnapshot Pet1 { get; init; }

    [Id(2)]
    public required PetBreedingPetSnapshot Pet2 { get; init; }

    [Id(3)]
    public required ImmutableArray<PetBreedingRarityCategorySnapshot> RarityCategories { get; init; }

    [Id(4)]
    public required int ResultPetTypeId { get; init; }
}
