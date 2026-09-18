using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Answer a love-lock confirmation.</summary>
[GenerateSerializer, Immutable]
public sealed record ConfirmFriendFurniLockInteraction : FurnitureInteraction
{
    [Id(0)]
    public required bool Confirmed { get; init; }
}
