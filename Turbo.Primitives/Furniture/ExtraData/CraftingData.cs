using System.Collections.Immutable;

namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// What a crafting table makes, under <see cref="SECTION"/> in its definition's extra data. The
/// recipes are the hotel's (Sulake's are not in its furni data). A listed recipe shows in the
/// table's recipe list; a secret one is only found by putting its exact ingredients in the mixer.
/// </summary>
public sealed record CraftingData
{
    public const string SECTION = "crafting";

    public ImmutableArray<CraftingRecipeData> Recipes { get; init; } = [];
}

public sealed record CraftingRecipeData
{
    /// <summary>The recipe's code, which the client sends back to ask for and craft it.</summary>
    public string Code { get; init; } = "";

    /// <summary>The furni class the recipe makes.</summary>
    public string Product { get; init; } = "";

    /// <summary>The product code the client names the product by; the furni class when empty.</summary>
    public string? ProductCode { get; init; }

    public bool Secret { get; init; }

    public ImmutableArray<CraftingIngredientData> Ingredients { get; init; } = [];
}

public sealed record CraftingIngredientData
{
    public string Furni { get; init; } = "";

    public int Amount { get; init; } = 1;
}
