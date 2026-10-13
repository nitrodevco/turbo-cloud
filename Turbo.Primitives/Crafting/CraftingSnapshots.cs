using Orleans;

namespace Turbo.Primitives.Crafting;

/// <summary>
/// A recipe as the crafting table lists it (Flash crafting/FurnitureProductItem.as:13): the recipe's
/// code, the product code the client looks its name up by, and the furni class it makes.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CraftableProductSnapshot
{
    [Id(0)]
    public required string RecipeCode { get; init; }

    [Id(1)]
    public required string ProductCode { get; init; }

    [Id(2)]
    public required string FurnitureClassName { get; init; }
}

/// <summary>One line of a recipe (Flash crafting/OutgoingIngredient.as:11): how many of which furni class.</summary>
[GenerateSerializer, Immutable]
public sealed record CraftingIngredientSnapshot
{
    [Id(0)]
    public required int Count { get; init; }

    [Id(1)]
    public required string FurnitureClassName { get; init; }
}
