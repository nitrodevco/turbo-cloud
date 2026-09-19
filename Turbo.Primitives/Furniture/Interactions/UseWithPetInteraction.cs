using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>A pet product in the room (saddle, revival potion) is applied to a pet.</summary>
[GenerateSerializer, Immutable]
public sealed record UseWithPetInteraction : FurnitureInteraction
{
    [Id(0)]
    public required int PetId { get; init; }
}
