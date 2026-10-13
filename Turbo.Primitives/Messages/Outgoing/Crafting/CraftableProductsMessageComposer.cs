using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Crafting;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Crafting;

/// <summary>
/// What a crafting table makes and which furni classes its mixer takes (Flash
/// CraftableProductsMessageParser.as:18). Empty on both counts, Flash hides the widget
/// (CraftingWidgetHandler.onCraftableProductsMessage).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CraftableProductsMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<CraftableProductSnapshot> Products { get; init; }

    [Id(1)]
    public required ImmutableArray<string> UsableFurniClasses { get; init; }
}
