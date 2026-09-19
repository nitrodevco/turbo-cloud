using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Set the look a clothing booth gives one gender.</summary>
[GenerateSerializer, Immutable]
public sealed record SetClothingChangeInteraction : FurnitureInteraction
{
    [Id(0)]
    public required AvatarGenderType Gender { get; init; }

    [Id(1)]
    public required string Figure { get; init; }
}
