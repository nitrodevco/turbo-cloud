using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>The crafting table opened: what it makes and which furni it takes (Flash GetCraftableProductsComposer).</summary>
[GenerateSerializer, Immutable]
public sealed record GetCraftableProductsInteraction : FurnitureInteraction;

/// <summary>Craft a listed recipe from the player's inventory (Flash CraftComposer).</summary>
[GenerateSerializer, Immutable]
public sealed record CraftInteraction : FurnitureInteraction
{
    [Id(0)]
    public required string RecipeCode { get; init; }
}

/// <summary>
/// The mixer's inventory items: <see cref="Craft"/> crafts the secret recipe they make (Flash
/// CraftSecretComposer); otherwise only how many recipes they fit is asked (Flash
/// GetCraftingRecipesAvailableComposer).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CraftingMixerInteraction : FurnitureInteraction
{
    [Id(0)]
    public required ImmutableArray<RoomObjectId> ItemIds { get; init; }

    [Id(1)]
    public required bool Craft { get; init; }
}
