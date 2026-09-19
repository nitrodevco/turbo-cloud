using Orleans;

namespace Turbo.Primitives.Pets.Snapshots;

/// <summary>One of the two pets in a nest, as the breeding confirmation shows it.</summary>
[GenerateSerializer, Immutable]
public sealed record PetBreedingPetSnapshot
{
    [Id(0)]
    public required int PetId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required int Level { get; init; }

    [Id(3)]
    public required string Figure { get; init; }

    [Id(4)]
    public required string OwnerName { get; init; }
}
