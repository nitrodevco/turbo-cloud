using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Crafting;

/// <summary>Flash's GetCraftingRecipeComposer sends the recipe code only, no table.</summary>
public record GetCraftingRecipeMessage : IMessageEvent
{
    public required string RecipeCode { get; init; }
}
