using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Crafting;

/// <summary>
/// What the mixer's items fit (Flash CraftingRecipesAvailableMessageParser.as:11, read by
/// CraftingInfoController.craftingSecretRecipesAvailable): whether they make a secret recipe
/// exactly, and how many other recipes they are part of.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CraftingRecipesAvailableMessageComposer : IComposer
{
    [Id(0)]
    public required int Count { get; init; }

    [Id(1)]
    public required bool RecipeComplete { get; init; }
}
