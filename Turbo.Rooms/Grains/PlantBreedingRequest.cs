using Turbo.Primitives.Players;

namespace Turbo.Rooms.Grains;

/// <summary>A monsterplant owner asked another plant's owner to breed; the answer is pending.</summary>
public sealed class PlantBreedingRequest
{
    public required int PetId { get; init; }
    public required int OtherPetId { get; init; }
    public required PlayerId RequesterId { get; init; }
    public required PlayerId OtherOwnerId { get; init; }

    public bool Involves(int petId) => petId == PetId || petId == OtherPetId;
}
