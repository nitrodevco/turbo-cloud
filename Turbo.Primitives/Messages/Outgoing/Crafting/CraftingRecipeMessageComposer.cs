using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Crafting;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Crafting;

/// <summary>What a recipe takes (Flash CraftingRecipeMessageParser.as:9).</summary>
[GenerateSerializer, Immutable]
public sealed record CraftingRecipeMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<CraftingIngredientSnapshot> Ingredients { get; init; }
}
